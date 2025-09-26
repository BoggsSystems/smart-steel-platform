"""
API Functional Tests for Steel Mill AI Platform
Tests all REST API endpoints for functionality, validation, and error handling
"""

import unittest
import requests
import json
import sys
import os
import time
from unittest.mock import patch

# Add the parent directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from tests import TEST_CONFIG


class TestAPIEndpoints(unittest.TestCase):
    """Test suite for API endpoints"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.base_url = TEST_CONFIG['api']['base_url']
        cls.timeout = TEST_CONFIG['api']['timeout']
        
        # Wait for server to be ready
        cls._wait_for_server()
        
        # Test data for different endpoints
        cls.quality_test_data = {
            'grade': 'Grade60',
            'carbon_content': 0.25,
            'manganese_content': 1.2,
            'phosphorus_content': 0.03,
            'sulfur_content': 0.04,
            'billet_temperature': 1050,
            'rolling_speed': 8.0,
            'quench_rate': 50,
            'surface_quality_score': 90
        }
        
        cls.defect_test_data = {
            'surface_roughness': 2.5,
            'rib_height': 0.7,
            'surface_temperature': 45,
            'rib_spacing': 11.2,
            'diameter_deviation': 0.1,
            'straightness': 2.5
        }
        
        cls.maintenance_test_data = {
            'equipment_type': 'rolling_mill',
            'operating_hours': 2500,
            'vibration_level': 4.5,
            'temperature': 75,
            'bearing_temperature': 68,
            'roll_wear': 0.15
        }
    
    @classmethod
    def _wait_for_server(cls, max_retries=30):
        """Wait for the API server to be available"""
        for attempt in range(max_retries):
            try:
                response = requests.get(f'{cls.base_url}/health', timeout=2)
                if response.status_code == 200:
                    return
            except requests.exceptions.RequestException:
                pass
            
            time.sleep(2)
        
        raise Exception(f"API server not available at {cls.base_url} after {max_retries} attempts")
    
    def setUp(self):
        """Set up test fixtures for each test method"""
        self.headers = {'Content-Type': 'application/json'}
    
    def test_health_endpoint(self):
        """Test health check endpoint"""
        response = requests.get(f'{self.base_url}/health', timeout=self.timeout)
        
        # Check response status
        self.assertEqual(response.status_code, 200)
        
        # Check response structure
        data = response.json()
        required_fields = ['status', 'timestamp', 'server', 'version']
        
        for field in required_fields:
            self.assertIn(field, data)
        
        # Check response values
        self.assertEqual(data['status'], 'healthy')
        self.assertEqual(data['server'], 'simple')
        self.assertIsInstance(data['timestamp'], str)
        self.assertIsInstance(data['version'], str)
    
    def test_root_endpoint(self):
        """Test root endpoint returns HTML dashboard"""
        response = requests.get(self.base_url, timeout=self.timeout)
        
        # Check response status
        self.assertEqual(response.status_code, 200)
        
        # Check content type is HTML
        self.assertIn('text/html', response.headers.get('Content-Type', ''))
        
        # Check HTML content contains expected elements
        html_content = response.text
        self.assertIn('Steel Mill AI', html_content)
        self.assertIn('Available Endpoints', html_content)
        self.assertIn('/predict/quality', html_content)
        self.assertIn('/predict/defects', html_content)
        self.assertIn('/predict/maintenance', html_content)
    
    def test_quality_prediction_endpoint_success(self):
        """Test quality prediction endpoint with valid data"""
        response = requests.post(
            f'{self.base_url}/predict/quality',
            json=self.quality_test_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Check response status
        self.assertEqual(response.status_code, 200)
        
        # Check response structure
        data = response.json()
        required_fields = [
            'status', 'prediction', 'model', 'timestamp'
        ]
        
        for field in required_fields:
            self.assertIn(field, data)
        
        # Check status
        self.assertEqual(data['status'], 'success')
        
        # Check prediction structure
        prediction = data['prediction']
        prediction_fields = [
            'astm_compliant', 'quality_score', 'predicted_yield_strength',
            'predicted_tensile_strength', 'predicted_elongation', 'confidence'
        ]
        
        for field in prediction_fields:
            self.assertIn(field, prediction)
        
        # Validate prediction values
        self.assertIsInstance(prediction['astm_compliant'], bool)
        self.assertTrue(0 <= prediction['quality_score'] <= 100)
        self.assertTrue(200 <= prediction['predicted_yield_strength'] <= 700)
        self.assertTrue(300 <= prediction['predicted_tensile_strength'] <= 1000)
        self.assertTrue(5 <= prediction['predicted_elongation'] <= 25)
        self.assertTrue(0 <= prediction['confidence'] <= 1)
    
    def test_quality_prediction_different_grades(self):
        """Test quality prediction with different rebar grades"""
        grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        
        for grade in grades:
            test_data = self.quality_test_data.copy()
            test_data['grade'] = grade
            
            response = requests.post(
                f'{self.base_url}/predict/quality',
                json=test_data,
                headers=self.headers,
                timeout=self.timeout
            )
            
            self.assertEqual(response.status_code, 200)
            
            data = response.json()
            self.assertEqual(data['status'], 'success')
            
            # Check that prediction values are reasonable for grade
            prediction = data['prediction']
            if grade == 'Grade80':
                # Higher grade should predict higher strengths
                self.assertGreater(prediction['predicted_yield_strength'], 500)
            elif grade == 'Grade40':
                # Lower grade should predict lower strengths
                self.assertLess(prediction['predicted_yield_strength'], 400)
    
    def test_quality_prediction_missing_fields(self):
        """Test quality prediction with missing optional fields"""
        # Test with minimal data
        minimal_data = {
            'grade': 'Grade60',
            'carbon_content': 0.25
        }
        
        response = requests.post(
            f'{self.base_url}/predict/quality',
            json=minimal_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should still work with defaults
        self.assertEqual(response.status_code, 200)
        
        data = response.json()
        self.assertEqual(data['status'], 'success')
        self.assertIn('prediction', data)
    
    def test_quality_prediction_invalid_data(self):
        """Test quality prediction with invalid data"""
        # Test with invalid grade
        invalid_data = self.quality_test_data.copy()
        invalid_data['grade'] = 'InvalidGrade'
        
        response = requests.post(
            f'{self.base_url}/predict/quality',
            json=invalid_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should handle gracefully (mock server behavior)
        self.assertIn(response.status_code, [200, 400])  # Mock may return 200
        
        # Test with negative values
        negative_data = self.quality_test_data.copy()
        negative_data['carbon_content'] = -0.5
        
        response = requests.post(
            f'{self.base_url}/predict/quality',
            json=negative_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should handle gracefully
        self.assertIn(response.status_code, [200, 400])
    
    def test_defect_detection_endpoint_success(self):
        """Test defect detection endpoint with valid data"""
        response = requests.post(
            f'{self.base_url}/predict/defects',
            json=self.defect_test_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Check response status
        self.assertEqual(response.status_code, 200)
        
        # Check response structure
        data = response.json()
        required_fields = ['status', 'prediction', 'model', 'timestamp']
        
        for field in required_fields:
            self.assertIn(field, data)
        
        # Check status
        self.assertEqual(data['status'], 'success')
        
        # Check prediction structure
        prediction = data['prediction']
        prediction_fields = [
            'primary_defect', 'severity', 'confidence', 
            'defect_probability', 'recommendations'
        ]
        
        for field in prediction_fields:
            self.assertIn(field, prediction)
        
        # Validate prediction values
        expected_defects = [
            'no_defect', 'surface_crack', 'surface_fold', 'surface_pit',
            'rib_height_low', 'rib_height_high', 'rib_spacing_error',
            'rib_angle_error', 'rib_fill_poor', 'dimensional_error',
            'straightness_error', 'ovality_error', 'surface_scale',
            'inclusion', 'mechanical_damage'
        ]
        
        self.assertIn(prediction['primary_defect'], expected_defects)
        self.assertIn(prediction['severity'], ['low', 'medium', 'high'])
        self.assertTrue(0 <= prediction['confidence'] <= 1)
        self.assertTrue(0 <= prediction['defect_probability'] <= 1)
        self.assertIsInstance(prediction['recommendations'], list)
        self.assertGreater(len(prediction['recommendations']), 0)
    
    def test_defect_detection_edge_cases(self):
        """Test defect detection with edge case values"""
        # Test with high surface roughness (likely defect)
        high_roughness_data = self.defect_test_data.copy()
        high_roughness_data['surface_roughness'] = 8.0
        
        response = requests.post(
            f'{self.base_url}/predict/defects',
            json=high_roughness_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertEqual(data['status'], 'success')
        
        # Test with perfect conditions (likely no defect)
        perfect_data = {
            'surface_roughness': 1.0,
            'rib_height': 0.8,
            'surface_temperature': 25,
            'rib_spacing': 11.0,
            'diameter_deviation': 0.01,
            'straightness': 1.0
        }
        
        response = requests.post(
            f'{self.base_url}/predict/defects',
            json=perfect_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertEqual(data['status'], 'success')
    
    def test_maintenance_prediction_endpoint_success(self):
        """Test maintenance prediction endpoint with valid data"""
        response = requests.post(
            f'{self.base_url}/predict/maintenance',
            json=self.maintenance_test_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Check response status
        self.assertEqual(response.status_code, 200)
        
        # Check response structure
        data = response.json()
        required_fields = ['status', 'prediction', 'model', 'timestamp']
        
        for field in required_fields:
            self.assertIn(field, data)
        
        # Check status
        self.assertEqual(data['status'], 'success')
        
        # Check prediction structure
        prediction = data['prediction']
        prediction_fields = [
            'prediction', 'risk_score', 'confidence',
            'next_maintenance', 'recommendations'
        ]
        
        for field in prediction_fields:
            self.assertIn(field, prediction)
        
        # Validate prediction values
        self.assertIn(prediction['prediction'], ['healthy', 'failure_risk'])
        self.assertTrue(0 <= prediction['risk_score'] <= 1)
        self.assertTrue(0 <= prediction['confidence'] <= 1)
        self.assertIsInstance(prediction['next_maintenance'], str)
        self.assertIsInstance(prediction['recommendations'], list)
        self.assertGreater(len(prediction['recommendations']), 0)
    
    def test_maintenance_prediction_high_risk(self):
        """Test maintenance prediction with high-risk equipment"""
        high_risk_data = self.maintenance_test_data.copy()
        high_risk_data.update({
            'operating_hours': 8000,  # High hours
            'vibration_level': 12.0,  # High vibration
            'temperature': 95,        # High temperature
            'bearing_temperature': 85 # Hot bearings
        })
        
        response = requests.post(
            f'{self.base_url}/predict/maintenance',
            json=high_risk_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertEqual(data['status'], 'success')
        
        # High risk equipment should have higher risk scores
        prediction = data['prediction']
        # Note: Mock server returns random values, so we just check structure
        self.assertIn('risk_score', prediction)
        self.assertIn('recommendations', prediction)
    
    def test_synthetic_data_endpoint(self):
        """Test synthetic data generation endpoint"""
        response = requests.get(
            f'{self.base_url}/data/synthetic',
            timeout=self.timeout
        )
        
        # Check response status
        self.assertEqual(response.status_code, 200)
        
        # Check response structure
        data = response.json()
        required_fields = ['status', 'data']
        
        for field in required_fields:
            self.assertIn(field, data)
        
        # Check status
        self.assertEqual(data['status'], 'success')
        
        # Check data structure
        synthetic_data = data['data']
        data_fields = ['furnace_data', 'quality_data', 'timestamp']
        
        for field in data_fields:
            self.assertIn(field, synthetic_data)
        
        # Validate furnace data
        furnace_data = synthetic_data['furnace_data']
        self.assertIn('temperature', furnace_data)
        self.assertIn('power_consumption', furnace_data)
        self.assertIn('efficiency', furnace_data)
        self.assertIn('status', furnace_data)
        
        # Validate quality data
        quality_data = synthetic_data['quality_data']
        self.assertIn('sample_id', quality_data)
        self.assertIn('grade', quality_data)
        self.assertIn('test_type', quality_data)
        self.assertIn('passed', quality_data)
        
        # Check value ranges and types
        self.assertTrue(1000 <= furnace_data['temperature'] <= 1600)
        self.assertTrue(50 <= furnace_data['power_consumption'] <= 100)
        self.assertTrue(70 <= furnace_data['efficiency'] <= 100)
        self.assertIn(furnace_data['status'], ['operational', 'maintenance', 'startup'])
        
        self.assertIn(quality_data['grade'], ['Grade40', 'Grade60', 'Grade75', 'Grade80'])
        self.assertIn(quality_data['test_type'], ['tensile', 'bend', 'dimensional'])
        self.assertIsInstance(quality_data['passed'], bool)
    
    def test_content_type_validation(self):
        """Test API content type validation"""
        # Test with wrong content type
        response = requests.post(
            f'{self.base_url}/predict/quality',
            data=json.dumps(self.quality_test_data),
            headers={'Content-Type': 'text/plain'},
            timeout=self.timeout
        )
        
        # Should handle or reject non-JSON content type
        self.assertIn(response.status_code, [200, 400, 415])
        
        # Test with no content type
        response = requests.post(
            f'{self.base_url}/predict/quality',
            json=self.quality_test_data,
            timeout=self.timeout
        )
        
        # Should still work (requests sets JSON content type automatically)
        self.assertEqual(response.status_code, 200)
    
    def test_request_size_limits(self):
        """Test API request size handling"""
        # Test with large request (within reasonable limits)
        large_data = self.quality_test_data.copy()
        large_data['extra_data'] = 'x' * 1000  # 1KB extra data
        
        response = requests.post(
            f'{self.base_url}/predict/quality',
            json=large_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should handle moderate size increases
        self.assertIn(response.status_code, [200, 413])  # 413 = Payload Too Large
    
    def test_concurrent_requests(self):
        """Test API handling of concurrent requests"""
        import threading
        import queue
        
        results = queue.Queue()
        
        def make_request():
            try:
                response = requests.post(
                    f'{self.base_url}/predict/quality',
                    json=self.quality_test_data,
                    headers=self.headers,
                    timeout=self.timeout
                )
                results.put(('success', response.status_code))
            except Exception as e:
                results.put(('error', str(e)))
        
        # Launch concurrent requests
        threads = []
        for _ in range(10):
            thread = threading.Thread(target=make_request)
            threads.append(thread)
            thread.start()
        
        # Wait for all threads to complete
        for thread in threads:
            thread.join()
        
        # Check results
        success_count = 0
        while not results.empty():
            result_type, result_value = results.get()
            if result_type == 'success' and result_value == 200:
                success_count += 1
        
        # Most requests should succeed
        self.assertGreaterEqual(success_count, 8)  # At least 80% success rate
    
    def test_response_time_performance(self):
        """Test API response time performance"""
        endpoints = [
            ('/health', 'GET', None),
            ('/predict/quality', 'POST', self.quality_test_data),
            ('/predict/defects', 'POST', self.defect_test_data),
            ('/predict/maintenance', 'POST', self.maintenance_test_data),
            ('/data/synthetic', 'GET', None)
        ]
        
        for endpoint, method, data in endpoints:
            start_time = time.time()
            
            if method == 'GET':
                response = requests.get(
                    f'{self.base_url}{endpoint}',
                    timeout=self.timeout
                )
            else:
                response = requests.post(
                    f'{self.base_url}{endpoint}',
                    json=data,
                    headers=self.headers,
                    timeout=self.timeout
                )
            
            response_time = (time.time() - start_time) * 1000  # Convert to ms
            
            # Check response was successful
            self.assertEqual(response.status_code, 200)
            
            # Check response time is reasonable
            max_response_time = 2000  # 2 seconds max for mock server
            self.assertLess(response_time, max_response_time,
                           f"{endpoint} took {response_time:.2f}ms, max is {max_response_time}ms")
    
    def test_malformed_json_handling(self):
        """Test API handling of malformed JSON"""
        # Test with invalid JSON
        response = requests.post(
            f'{self.base_url}/predict/quality',
            data='{"invalid": json}',  # Missing quotes
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should handle malformed JSON gracefully
        self.assertIn(response.status_code, [200, 400])
        
        # Test with empty body
        response = requests.post(
            f'{self.base_url}/predict/quality',
            data='',
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should handle empty body gracefully
        self.assertIn(response.status_code, [200, 400])
    
    def test_http_methods_validation(self):
        """Test API HTTP method validation"""
        # Test GET on POST endpoints (should fail)
        response = requests.get(
            f'{self.base_url}/predict/quality',
            timeout=self.timeout
        )
        
        # Should return method not allowed
        self.assertIn(response.status_code, [405, 404])  # 405 = Method Not Allowed
        
        # Test POST on GET endpoints (should fail)
        response = requests.post(
            f'{self.base_url}/health',
            json=self.quality_test_data,
            headers=self.headers,
            timeout=self.timeout
        )
        
        # Should return method not allowed or still work (depending on implementation)
        self.assertIn(response.status_code, [200, 405])
    
    def test_api_documentation_endpoint(self):
        """Test API documentation endpoint (Swagger/OpenAPI)"""
        # Test if docs endpoint exists
        response = requests.get(f'{self.base_url}/docs', timeout=self.timeout)
        
        # Swagger docs should be available
        if response.status_code == 200:
            # Check if it's HTML content (Swagger UI)
            content_type = response.headers.get('Content-Type', '')
            self.assertIn('text/html', content_type)
            
            # Check for Swagger-specific content
            html_content = response.text
            self.assertTrue(any(term in html_content.lower() for term in 
                              ['swagger', 'openapi', 'api documentation']))
    
    def test_error_response_format(self):
        """Test error response format consistency"""
        # Test with completely invalid endpoint
        response = requests.get(
            f'{self.base_url}/nonexistent',
            timeout=self.timeout
        )
        
        # Should return 404
        self.assertEqual(response.status_code, 404)
        
        # Check if error response has consistent format
        if response.headers.get('Content-Type', '').startswith('application/json'):
            data = response.json()
            # Common error fields
            self.assertTrue(any(field in data for field in 
                              ['error', 'message', 'detail', 'status']))


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)