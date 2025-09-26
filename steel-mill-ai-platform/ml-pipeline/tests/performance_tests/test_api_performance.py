"""
API Performance Tests for Steel Mill AI Platform
Tests response times, throughput, concurrent load handling, and resource usage
"""

import unittest
import requests
import time
import threading
import statistics
import sys
import os
import psutil
from concurrent.futures import ThreadPoolExecutor, as_completed

# Add the parent directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from tests import TEST_CONFIG


class TestAPIPerformance(unittest.TestCase):
    """Test suite for API performance"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.base_url = TEST_CONFIG['api']['base_url']
        cls.timeout = TEST_CONFIG['api']['timeout']
        cls.max_concurrent = TEST_CONFIG['api']['max_concurrent_requests']
        
        # Performance targets from config
        cls.max_response_time = 200  # milliseconds
        cls.max_batch_time_per_item = 50  # milliseconds per item
        cls.min_throughput = 10  # requests per second
        
        # Test data
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
        
        # Ensure server is available
        cls._wait_for_server()
    
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
            time.sleep(1)
        
        raise Exception(f"API server not available at {cls.base_url}")
    
    def setUp(self):
        """Set up test fixtures for each test method"""
        self.headers = {'Content-Type': 'application/json'}
        self.performance_results = {}
    
    def _measure_response_time(self, method, endpoint, data=None, iterations=10):
        """Measure average response time for an endpoint"""
        response_times = []
        
        for _ in range(iterations):
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
            response_times.append(response_time)
            
            # Ensure request was successful
            self.assertEqual(response.status_code, 200)
            
            # Small delay between requests
            time.sleep(0.1)
        
        return {
            'avg': statistics.mean(response_times),
            'min': min(response_times),
            'max': max(response_times),
            'p95': statistics.quantiles(response_times, n=20)[18],  # 95th percentile
            'p99': statistics.quantiles(response_times, n=100)[98],  # 99th percentile
            'all_times': response_times
        }
    
    def test_health_endpoint_performance(self):
        """Test health endpoint response time"""
        results = self._measure_response_time('GET', '/health', iterations=20)
        
        # Store results for reporting
        self.performance_results['health'] = results
        
        # Check performance targets
        self.assertLess(results['avg'], self.max_response_time,
                       f"Average response time {results['avg']:.2f}ms exceeds target {self.max_response_time}ms")
        
        self.assertLess(results['p95'], self.max_response_time * 2,
                       f"95th percentile {results['p95']:.2f}ms exceeds target {self.max_response_time * 2}ms")
        
        print(f"\n📊 Health Endpoint Performance:")
        print(f"   Average: {results['avg']:.2f}ms")
        print(f"   95th percentile: {results['p95']:.2f}ms")
        print(f"   99th percentile: {results['p99']:.2f}ms")
    
    def test_quality_prediction_performance(self):
        """Test quality prediction endpoint response time"""
        results = self._measure_response_time('POST', '/predict/quality', 
                                            self.quality_test_data, iterations=20)
        
        # Store results for reporting
        self.performance_results['quality_prediction'] = results
        
        # Check performance targets
        self.assertLess(results['avg'], self.max_response_time,
                       f"Average response time {results['avg']:.2f}ms exceeds target {self.max_response_time}ms")
        
        self.assertLess(results['p95'], self.max_response_time * 3,
                       f"95th percentile {results['p95']:.2f}ms exceeds target {self.max_response_time * 3}ms")
        
        print(f"\n📊 Quality Prediction Performance:")
        print(f"   Average: {results['avg']:.2f}ms")
        print(f"   95th percentile: {results['p95']:.2f}ms")
        print(f"   99th percentile: {results['p99']:.2f}ms")
    
    def test_defect_detection_performance(self):
        """Test defect detection endpoint response time"""
        results = self._measure_response_time('POST', '/predict/defects',
                                            self.defect_test_data, iterations=20)
        
        # Store results for reporting
        self.performance_results['defect_detection'] = results
        
        # Check performance targets
        self.assertLess(results['avg'], self.max_response_time,
                       f"Average response time {results['avg']:.2f}ms exceeds target {self.max_response_time}ms")
        
        print(f"\n📊 Defect Detection Performance:")
        print(f"   Average: {results['avg']:.2f}ms")
        print(f"   95th percentile: {results['p95']:.2f}ms")
        print(f"   99th percentile: {results['p99']:.2f}ms")
    
    def test_maintenance_prediction_performance(self):
        """Test maintenance prediction endpoint response time"""
        results = self._measure_response_time('POST', '/predict/maintenance',
                                            self.maintenance_test_data, iterations=20)
        
        # Store results for reporting
        self.performance_results['maintenance_prediction'] = results
        
        # Check performance targets
        self.assertLess(results['avg'], self.max_response_time,
                       f"Average response time {results['avg']:.2f}ms exceeds target {self.max_response_time}ms")
        
        print(f"\n📊 Maintenance Prediction Performance:")
        print(f"   Average: {results['avg']:.2f}ms")
        print(f"   95th percentile: {results['p95']:.2f}ms")
        print(f"   99th percentile: {results['p99']:.2f}ms")
    
    def test_synthetic_data_performance(self):
        """Test synthetic data endpoint response time"""
        results = self._measure_response_time('GET', '/data/synthetic', iterations=20)
        
        # Store results for reporting
        self.performance_results['synthetic_data'] = results
        
        # Check performance targets
        self.assertLess(results['avg'], self.max_response_time,
                       f"Average response time {results['avg']:.2f}ms exceeds target {self.max_response_time}ms")
        
        print(f"\n📊 Synthetic Data Performance:")
        print(f"   Average: {results['avg']:.2f}ms")
        print(f"   95th percentile: {results['p95']:.2f}ms")
        print(f"   99th percentile: {results['p99']:.2f}ms")
    
    def test_concurrent_request_handling(self):
        """Test API performance under concurrent load"""
        def make_request(request_id):
            start_time = time.time()
            try:
                response = requests.post(
                    f'{self.base_url}/predict/quality',
                    json=self.quality_test_data,
                    headers=self.headers,
                    timeout=self.timeout
                )
                response_time = (time.time() - start_time) * 1000
                return {
                    'id': request_id,
                    'success': response.status_code == 200,
                    'response_time': response_time,
                    'status_code': response.status_code
                }
            except Exception as e:
                response_time = (time.time() - start_time) * 1000
                return {
                    'id': request_id,
                    'success': False,
                    'response_time': response_time,
                    'error': str(e)
                }
        
        # Test with different concurrent loads
        concurrent_loads = [5, 10, 20]
        
        for concurrent_requests in concurrent_loads:
            print(f"\n🔄 Testing {concurrent_requests} concurrent requests...")
            
            start_time = time.time()
            
            with ThreadPoolExecutor(max_workers=concurrent_requests) as executor:
                futures = [executor.submit(make_request, i) for i in range(concurrent_requests)]
                results = [future.result() for future in as_completed(futures)]
            
            total_time = time.time() - start_time
            
            # Analyze results
            successful_requests = [r for r in results if r['success']]
            failed_requests = [r for r in results if not r['success']]
            
            success_rate = len(successful_requests) / len(results)
            avg_response_time = statistics.mean([r['response_time'] for r in successful_requests]) if successful_requests else 0
            throughput = len(successful_requests) / total_time
            
            print(f"   Success rate: {success_rate:.2%}")
            print(f"   Average response time: {avg_response_time:.2f}ms")
            print(f"   Throughput: {throughput:.1f} requests/second")
            
            # Performance assertions
            self.assertGreaterEqual(success_rate, 0.95, 
                                   f"Success rate {success_rate:.2%} below 95% for {concurrent_requests} concurrent requests")
            
            if successful_requests:
                self.assertLess(avg_response_time, self.max_response_time * 2,
                               f"Average response time {avg_response_time:.2f}ms exceeds {self.max_response_time * 2}ms under load")
    
    def test_throughput_measurement(self):
        """Test API throughput (requests per second)"""
        def make_quality_request():
            try:
                response = requests.post(
                    f'{self.base_url}/predict/quality',
                    json=self.quality_test_data,
                    headers=self.headers,
                    timeout=self.timeout
                )
                return response.status_code == 200
            except:
                return False
        
        # Measure throughput for 30 seconds with 5 concurrent threads
        duration = 30  # seconds
        concurrent_threads = 5
        
        print(f"\n⚡ Measuring throughput for {duration} seconds with {concurrent_threads} threads...")
        
        successful_requests = 0
        total_requests = 0
        start_time = time.time()
        end_time = start_time + duration
        
        def worker():
            nonlocal successful_requests, total_requests
            while time.time() < end_time:
                if make_quality_request():
                    successful_requests += 1
                total_requests += 1
                time.sleep(0.1)  # Small delay between requests
        
        # Start worker threads
        threads = []
        for _ in range(concurrent_threads):
            thread = threading.Thread(target=worker)
            threads.append(thread)
            thread.start()
        
        # Wait for all threads to complete
        for thread in threads:
            thread.join()
        
        actual_duration = time.time() - start_time
        throughput = successful_requests / actual_duration
        success_rate = successful_requests / total_requests if total_requests > 0 else 0
        
        print(f"   Total requests: {total_requests}")
        print(f"   Successful requests: {successful_requests}")
        print(f"   Success rate: {success_rate:.2%}")
        print(f"   Throughput: {throughput:.2f} requests/second")
        
        # Throughput should meet minimum requirement
        self.assertGreaterEqual(throughput, self.min_throughput,
                               f"Throughput {throughput:.2f} req/sec below target {self.min_throughput} req/sec")
        
        # Success rate should be high
        self.assertGreaterEqual(success_rate, 0.95,
                               f"Success rate {success_rate:.2%} below 95%")
    
    def test_load_testing(self):
        """Test API under sustained load"""
        def stress_worker(worker_id, results):
            """Worker function for stress testing"""
            request_count = 0
            successful_requests = 0
            response_times = []
            
            # Run for 60 seconds
            end_time = time.time() + 60
            
            while time.time() < end_time:
                start_time = time.time()
                try:
                    response = requests.post(
                        f'{self.base_url}/predict/quality',
                        json=self.quality_test_data,
                        headers=self.headers,
                        timeout=self.timeout
                    )
                    response_time = (time.time() - start_time) * 1000
                    response_times.append(response_time)
                    
                    if response.status_code == 200:
                        successful_requests += 1
                    
                    request_count += 1
                    
                except Exception as e:
                    request_count += 1
                    # Continue testing even if some requests fail
                
                # Small delay to control load
                time.sleep(0.05)
            
            results[worker_id] = {
                'total_requests': request_count,
                'successful_requests': successful_requests,
                'response_times': response_times
            }
        
        print(f"\n🔥 Load testing with 10 concurrent workers for 60 seconds...")
        
        # Start load test
        num_workers = 10
        results = {}
        threads = []
        
        start_time = time.time()
        
        for worker_id in range(num_workers):
            thread = threading.Thread(target=stress_worker, args=(worker_id, results))
            threads.append(thread)
            thread.start()
        
        # Wait for all workers to complete
        for thread in threads:
            thread.join()
        
        total_duration = time.time() - start_time
        
        # Aggregate results
        total_requests = sum(r['total_requests'] for r in results.values())
        total_successful = sum(r['successful_requests'] for r in results.values())
        all_response_times = []
        for r in results.values():
            all_response_times.extend(r['response_times'])
        
        success_rate = total_successful / total_requests if total_requests > 0 else 0
        throughput = total_successful / total_duration
        avg_response_time = statistics.mean(all_response_times) if all_response_times else 0
        
        print(f"   Duration: {total_duration:.1f} seconds")
        print(f"   Total requests: {total_requests}")
        print(f"   Successful requests: {total_successful}")
        print(f"   Success rate: {success_rate:.2%}")
        print(f"   Throughput: {throughput:.2f} requests/second")
        print(f"   Average response time: {avg_response_time:.2f}ms")
        
        # Load test assertions
        self.assertGreaterEqual(success_rate, 0.90,
                               f"Success rate {success_rate:.2%} below 90% under load")
        
        if all_response_times:
            p95_response_time = statistics.quantiles(all_response_times, n=20)[18]
            print(f"   95th percentile response time: {p95_response_time:.2f}ms")
            
            self.assertLess(avg_response_time, self.max_response_time * 3,
                           f"Average response time {avg_response_time:.2f}ms too high under load")
    
    def test_memory_usage_monitoring(self):
        """Test API memory usage during operation"""
        # Note: This test monitors the test process, not the API server process
        # In a real deployment, you would monitor the API server process
        
        print(f"\n💾 Monitoring memory usage during API requests...")
        
        process = psutil.Process()
        initial_memory = process.memory_info().rss / 1024 / 1024  # MB
        
        # Make many requests to see if memory usage grows
        for i in range(100):
            response = requests.post(
                f'{self.base_url}/predict/quality',
                json=self.quality_test_data,
                headers=self.headers,
                timeout=self.timeout
            )
            
            if i % 20 == 0:
                current_memory = process.memory_info().rss / 1024 / 1024  # MB
                print(f"   After {i} requests: {current_memory:.1f}MB")
            
            self.assertEqual(response.status_code, 200)
            time.sleep(0.01)  # Small delay
        
        final_memory = process.memory_info().rss / 1024 / 1024  # MB
        memory_increase = final_memory - initial_memory
        
        print(f"   Initial memory: {initial_memory:.1f}MB")
        print(f"   Final memory: {final_memory:.1f}MB")
        print(f"   Memory increase: {memory_increase:.1f}MB")
        
        # Memory increase should be reasonable
        self.assertLess(memory_increase, 50,  # Less than 50MB increase
                       f"Memory increase {memory_increase:.1f}MB seems excessive")
    
    def test_error_handling_performance(self):
        """Test performance of error handling"""
        # Test invalid requests to ensure error handling doesn't slow down the system
        invalid_data = {'invalid': 'data', 'bad_field': -999}
        
        results = self._measure_response_time('POST', '/predict/quality',
                                            invalid_data, iterations=10)
        
        print(f"\n⚠️  Error Handling Performance:")
        print(f"   Average error response time: {results['avg']:.2f}ms")
        
        # Error responses should still be fast
        self.assertLess(results['avg'], self.max_response_time * 2,
                       f"Error handling too slow: {results['avg']:.2f}ms")
    
    def tearDown(self):
        """Clean up after each test"""
        # Print performance summary if available
        if hasattr(self, 'performance_results') and self.performance_results:
            print(f"\n📈 Performance Summary:")
            for endpoint, results in self.performance_results.items():
                print(f"   {endpoint}: {results['avg']:.2f}ms avg, {results['p95']:.2f}ms p95")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)