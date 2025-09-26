"""
Batch Processing Validation Tests for Steel Mill AI Platform
Tests high-volume data processing, batch predictions, and throughput validation
"""

import unittest
import numpy as np
import pandas as pd
import sys
import os
import time
import concurrent.futures
from multiprocessing import Pool, cpu_count
import threading
from typing import List, Dict, Tuple

# Add the parent directory to the path to import our models
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from models.rebar_quality_prediction import RebarQualityPredictionModel
from models.rebar_defect_detection import RebarDefectDetectionModel
from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from tests import TEST_CONFIG


class TestBatchProcessing(unittest.TestCase):
    """Test suite for batch processing validation"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        # Initialize and train models
        cls.quality_model = RebarQualityPredictionModel()
        cls.defect_model = RebarDefectDetectionModel()
        cls.maintenance_model = RebarPredictiveMaintenanceModel()
        
        # Train models with moderate datasets
        print("🤖 Training models for batch processing tests...")
        cls._train_models()
        
        # Performance targets
        cls.min_throughput = 100  # predictions per second
        cls.max_memory_growth = 100  # MB
        cls.max_processing_time_per_item = 0.1  # seconds
    
    @classmethod
    def _train_models(cls):
        """Train all models for batch processing tests"""
        # Quality model
        quality_data = cls.quality_model.generate_synthetic_training_data(n_samples=1000)
        quality_targets = {
            'compliance': 'astm_compliant',
            'yield_strength': 'actual_yield_strength',
            'tensile_strength': 'actual_tensile_strength',
            'elongation': 'actual_elongation'
        }
        cls.quality_model.train(quality_data, quality_targets)
        
        # Defect model  
        defect_data = cls.defect_model.generate_synthetic_training_data(n_samples=1000)
        cls.defect_model.train(defect_data, 'primary_defect')
        
        # Maintenance model
        maintenance_data = cls.maintenance_model.generate_synthetic_training_data(n_samples=1000)
        maintenance_targets = {
            'failure_prediction': 'failure_within_days',
            'rul_estimation': 'remaining_useful_life',
            'maintenance_type': 'recommended_maintenance'
        }
        cls.maintenance_model.train(maintenance_data, maintenance_targets)
        
        print("✅ Models trained successfully")
    
    def test_quality_batch_prediction_performance(self):
        """Test batch quality prediction performance and throughput"""
        batch_sizes = [100, 500, 1000, 2000]
        performance_results = {}
        
        for batch_size in batch_sizes:
            print(f"🔄 Testing quality batch prediction with {batch_size} samples...")
            
            # Generate test batch
            test_data = self.quality_model.generate_synthetic_training_data(n_samples=batch_size)
            
            # Measure batch prediction time
            start_time = time.time()
            results = self.quality_model.batch_predict(test_data)
            processing_time = time.time() - start_time
            
            # Calculate metrics
            throughput = batch_size / processing_time
            time_per_item = processing_time / batch_size
            
            performance_results[batch_size] = {
                'processing_time': processing_time,
                'throughput': throughput,
                'time_per_item': time_per_item
            }
            
            # Validate results structure
            self.assertEqual(len(results), batch_size)
            self.assertIn('astm_compliant', results.columns)
            self.assertIn('quality_score', results.columns)
            self.assertIn('predicted_yield_strength', results.columns)
            
            # Validate performance
            self.assertLess(time_per_item, self.max_processing_time_per_item,
                           f"Processing time {time_per_item:.4f}s per item too slow")
            
            # Validate prediction quality
            self.assertTrue(results['quality_score'].between(0, 100).all())
            self.assertTrue(results['compliance_confidence'].between(0, 1).all())
            
            print(f"   ✅ Batch size {batch_size}: {throughput:.1f} predictions/sec, {time_per_item*1000:.1f}ms per item")
        
        # Check throughput scaling
        largest_batch = max(batch_sizes)
        self.assertGreater(performance_results[largest_batch]['throughput'], self.min_throughput,
                          f"Throughput {performance_results[largest_batch]['throughput']:.1f} below target {self.min_throughput}")
        
        print(f"✅ Quality Batch Performance Summary:")
        for batch_size, metrics in performance_results.items():
            print(f"   {batch_size:,} samples: {metrics['throughput']:.0f} pred/sec")
    
    def test_defect_batch_detection_performance(self):
        """Test batch defect detection performance"""
        batch_sizes = [100, 500, 1000]
        
        for batch_size in batch_sizes:
            print(f"🔄 Testing defect batch detection with {batch_size} samples...")
            
            # Generate test batch
            test_data = self.defect_model.generate_synthetic_training_data(n_samples=batch_size)
            
            # Measure batch processing time
            start_time = time.time()
            results = self.defect_model.batch_detect(test_data)
            processing_time = time.time() - start_time
            
            throughput = batch_size / processing_time
            time_per_item = processing_time / batch_size
            
            # Validate results
            self.assertEqual(len(results), batch_size)
            self.assertIn('primary_defect', results.columns)
            self.assertIn('severity', results.columns)
            self.assertIn('defect_probability', results.columns)
            
            # Validate performance
            self.assertLess(time_per_item, self.max_processing_time_per_item * 1.5)  # More lenient for defect detection
            
            # Validate prediction values
            self.assertTrue(results['defect_probability'].between(0, 1).all())
            self.assertTrue(results['severity'].isin(['low', 'medium', 'high']).all())
            
            print(f"   ✅ Batch size {batch_size}: {throughput:.1f} detections/sec")
    
    def test_maintenance_batch_prediction_performance(self):
        """Test batch maintenance prediction performance"""
        batch_sizes = [50, 200, 500]  # Smaller batches due to equipment-specific processing
        
        for batch_size in batch_sizes:
            print(f"🔄 Testing maintenance batch prediction with {batch_size} samples...")
            
            # Generate test batch
            test_data = self.maintenance_model.generate_synthetic_training_data(n_samples=batch_size)
            
            # Measure batch processing time
            start_time = time.time()
            results = self.maintenance_model.batch_predict(test_data)
            processing_time = time.time() - start_time
            
            throughput = batch_size / processing_time
            time_per_item = processing_time / batch_size
            
            # Validate results
            self.assertEqual(len(results), batch_size)
            self.assertIn('failure_probability', results.columns)
            self.assertIn('estimated_rul_days', results.columns)
            self.assertIn('risk_level', results.columns)
            
            # Validate performance (more lenient for maintenance)
            self.assertLess(time_per_item, self.max_processing_time_per_item * 2)
            
            # Validate prediction values
            self.assertTrue(results['failure_probability'].between(0, 1).all())
            self.assertTrue(results['estimated_rul_days'].ge(0).all())
            self.assertTrue(results['risk_level'].isin(['low', 'medium', 'high', 'critical']).all())
            
            print(f"   ✅ Batch size {batch_size}: {throughput:.1f} predictions/sec")
    
    def test_concurrent_batch_processing(self):
        """Test concurrent processing of multiple batches"""
        print("🔄 Testing concurrent batch processing...")
        
        def process_quality_batch(batch_id: int) -> Dict:
            """Process a single quality batch"""
            batch_size = 200
            test_data = self.quality_model.generate_synthetic_training_data(n_samples=batch_size)
            
            start_time = time.time()
            results = self.quality_model.batch_predict(test_data)
            processing_time = time.time() - start_time
            
            return {
                'batch_id': batch_id,
                'batch_size': batch_size,
                'processing_time': processing_time,
                'throughput': batch_size / processing_time,
                'results_count': len(results)
            }
        
        # Test concurrent processing with ThreadPoolExecutor
        num_concurrent_batches = 5
        concurrent_results = []
        
        start_time = time.time()
        
        with concurrent.futures.ThreadPoolExecutor(max_workers=num_concurrent_batches) as executor:
            futures = [executor.submit(process_quality_batch, i) for i in range(num_concurrent_batches)]
            concurrent_results = [future.result() for future in concurrent.futures.as_completed(futures)]
        
        total_concurrent_time = time.time() - start_time
        
        # Process sequentially for comparison
        sequential_results = []
        start_time = time.time()
        
        for i in range(num_concurrent_batches):
            result = process_quality_batch(i)
            sequential_results.append(result)
        
        total_sequential_time = time.time() - start_time
        
        # Calculate metrics
        total_samples = sum(r['batch_size'] for r in concurrent_results)
        concurrent_throughput = total_samples / total_concurrent_time
        sequential_throughput = total_samples / total_sequential_time
        
        speedup = total_sequential_time / total_concurrent_time
        
        # Validate concurrent processing
        self.assertEqual(len(concurrent_results), num_concurrent_batches)
        for result in concurrent_results:
            self.assertEqual(result['results_count'], result['batch_size'])
        
        # Concurrent should be faster (or at least not much slower)
        self.assertGreater(speedup, 0.8, "Concurrent processing should provide speedup")
        
        print(f"✅ Concurrent Batch Processing:")
        print(f"   Concurrent batches: {num_concurrent_batches}")
        print(f"   Total samples: {total_samples:,}")
        print(f"   Concurrent throughput: {concurrent_throughput:.1f} pred/sec")
        print(f"   Sequential throughput: {sequential_throughput:.1f} pred/sec")
        print(f"   Speedup: {speedup:.2f}x")
    
    def test_memory_usage_during_batch_processing(self):
        """Test memory usage during large batch processing"""
        import psutil
        import gc
        
        print("🔄 Testing memory usage during batch processing...")
        
        # Get initial memory usage
        process = psutil.Process()
        initial_memory = process.memory_info().rss / 1024 / 1024  # MB
        
        batch_sizes = [500, 1000, 2000, 3000]
        memory_usage = []
        
        for batch_size in batch_sizes:
            # Clear memory before each test
            gc.collect()
            
            # Generate large batch
            test_data = self.quality_model.generate_synthetic_training_data(n_samples=batch_size)
            
            # Measure memory before processing
            memory_before = process.memory_info().rss / 1024 / 1024
            
            # Process batch
            results = self.quality_model.batch_predict(test_data)
            
            # Measure memory after processing
            memory_after = process.memory_info().rss / 1024 / 1024
            memory_increase = memory_after - memory_before
            
            memory_usage.append({
                'batch_size': batch_size,
                'memory_before': memory_before,
                'memory_after': memory_after,
                'memory_increase': memory_increase,
                'memory_per_sample': memory_increase / batch_size if batch_size > 0 else 0
            })
            
            # Clean up
            del test_data
            del results
            gc.collect()
        
        final_memory = process.memory_info().rss / 1024 / 1024
        total_memory_growth = final_memory - initial_memory
        
        # Validate memory usage
        self.assertLess(total_memory_growth, self.max_memory_growth,
                       f"Total memory growth {total_memory_growth:.1f}MB exceeds limit")
        
        # Memory usage should not grow excessively per sample
        for usage in memory_usage:
            self.assertLess(usage['memory_per_sample'], 0.1,  # 0.1 MB per sample
                           f"Memory per sample {usage['memory_per_sample']:.3f}MB too high")
        
        print(f"✅ Memory Usage Analysis:")
        print(f"   Initial memory: {initial_memory:.1f}MB")
        print(f"   Final memory: {final_memory:.1f}MB")
        print(f"   Total growth: {total_memory_growth:.1f}MB")
        
        for usage in memory_usage:
            print(f"   {usage['batch_size']:,} samples: "
                  f"+{usage['memory_increase']:.1f}MB "
                  f"({usage['memory_per_sample']*1000:.1f}KB per sample)")
    
    def test_streaming_batch_processing(self):
        """Test streaming-style batch processing with continuous data flow"""
        print("🔄 Testing streaming batch processing...")
        
        # Simulate continuous data stream
        stream_duration = 30  # seconds
        batch_size = 50
        batch_interval = 2  # seconds between batches
        
        processed_batches = []
        total_predictions = 0
        start_time = time.time()
        
        while time.time() - start_time < stream_duration:
            # Generate streaming batch
            batch_data = self.quality_model.generate_synthetic_training_data(n_samples=batch_size)
            
            # Process batch with timing
            batch_start = time.time()
            results = self.quality_model.batch_predict(batch_data)
            batch_processing_time = time.time() - batch_start
            
            processed_batches.append({
                'timestamp': time.time(),
                'batch_size': batch_size,
                'processing_time': batch_processing_time,
                'throughput': batch_size / batch_processing_time
            })
            
            total_predictions += batch_size
            
            # Wait for next batch (simulate real-time constraint)
            time.sleep(max(0, batch_interval - batch_processing_time))
        
        total_time = time.time() - start_time
        overall_throughput = total_predictions / total_time
        
        # Calculate streaming metrics
        avg_batch_time = np.mean([b['processing_time'] for b in processed_batches])
        avg_throughput = np.mean([b['throughput'] for b in processed_batches])
        
        # Validate streaming performance
        self.assertGreater(len(processed_batches), 10, "Should process multiple batches")
        self.assertLess(avg_batch_time, batch_interval * 0.8,
                       "Batch processing should be faster than interval")
        
        print(f"✅ Streaming Batch Processing:")
        print(f"   Stream duration: {total_time:.1f}s")
        print(f"   Batches processed: {len(processed_batches)}")
        print(f"   Total predictions: {total_predictions:,}")
        print(f"   Overall throughput: {overall_throughput:.1f} pred/sec")
        print(f"   Average batch time: {avg_batch_time:.3f}s")
        print(f"   Average batch throughput: {avg_throughput:.1f} pred/sec")
    
    def test_batch_processing_accuracy_consistency(self):
        """Test that batch processing maintains prediction accuracy"""
        print("🔄 Testing batch vs single prediction consistency...")
        
        # Generate test data
        test_size = 100
        test_data = self.quality_model.generate_synthetic_training_data(n_samples=test_size)
        
        # Process as single predictions
        single_predictions = []
        for _, row in test_data.iterrows():
            row_dict = row.to_dict()
            prediction = self.quality_model.predict_quality(row_dict)
            single_predictions.append(prediction)
        
        # Process as batch
        batch_results = self.quality_model.batch_predict(test_data)
        
        # Compare results
        prediction_differences = []
        for i, single_pred in enumerate(single_predictions):
            batch_pred = {
                'astm_compliant': batch_results.iloc[i]['astm_compliant'],
                'quality_score': batch_results.iloc[i]['quality_score'],
                'predicted_yield_strength': batch_results.iloc[i]['predicted_yield_strength']
            }
            
            # Calculate differences
            quality_score_diff = abs(single_pred['quality_score'] - batch_pred['quality_score'])
            yield_strength_diff = abs(single_pred['predicted_yield_strength'] - 
                                    batch_pred['predicted_yield_strength'])
            
            prediction_differences.append({
                'quality_score_diff': quality_score_diff,
                'yield_strength_diff': yield_strength_diff,
                'compliance_match': single_pred['astm_compliant'] == batch_pred['astm_compliant']
            })
        
        # Calculate consistency metrics
        avg_quality_diff = np.mean([d['quality_score_diff'] for d in prediction_differences])
        avg_yield_diff = np.mean([d['yield_strength_diff'] for d in prediction_differences])
        compliance_match_rate = np.mean([d['compliance_match'] for d in prediction_differences])
        
        # Validate consistency
        self.assertLess(avg_quality_diff, 0.1, "Quality score differences too large")
        self.assertLess(avg_yield_diff, 1.0, "Yield strength differences too large")
        self.assertGreater(compliance_match_rate, 0.98, "Compliance predictions should match")
        
        print(f"✅ Batch vs Single Prediction Consistency:")
        print(f"   Samples compared: {test_size}")
        print(f"   Average quality score difference: {avg_quality_diff:.3f}")
        print(f"   Average yield strength difference: {avg_yield_diff:.1f} MPa")
        print(f"   Compliance match rate: {compliance_match_rate:.1%}")
    
    def test_batch_error_handling(self):
        """Test batch processing error handling and recovery"""
        print("🔄 Testing batch processing error handling...")
        
        # Test with various problematic data scenarios
        error_scenarios = []
        
        # Scenario 1: Batch with missing values
        test_data_missing = self.quality_model.generate_synthetic_training_data(n_samples=100)
        test_data_missing.loc[10:20, 'carbon_content'] = np.nan
        test_data_missing.loc[30:35, 'grade'] = None
        
        try:
            results = self.quality_model.batch_predict(test_data_missing)
            error_scenarios.append(('missing_values', True, len(results)))
        except Exception as e:
            error_scenarios.append(('missing_values', False, str(e)))
        
        # Scenario 2: Batch with extreme outliers
        test_data_outliers = self.quality_model.generate_synthetic_training_data(n_samples=100)
        test_data_outliers.loc[0, 'carbon_content'] = 10.0  # Extreme outlier
        test_data_outliers.loc[1, 'billet_temperature'] = -1000  # Impossible value
        
        try:
            results = self.quality_model.batch_predict(test_data_outliers)
            error_scenarios.append(('outliers', True, len(results)))
        except Exception as e:
            error_scenarios.append(('outliers', False, str(e)))
        
        # Scenario 3: Empty batch
        empty_data = pd.DataFrame(columns=test_data_missing.columns)
        
        try:
            results = self.quality_model.batch_predict(empty_data)
            error_scenarios.append(('empty_batch', True, len(results)))
        except Exception as e:
            error_scenarios.append(('empty_batch', False, str(e)))
        
        # Scenario 4: Single row batch
        single_row_data = self.quality_model.generate_synthetic_training_data(n_samples=1)
        
        try:
            results = self.quality_model.batch_predict(single_row_data)
            error_scenarios.append(('single_row', True, len(results)))
        except Exception as e:
            error_scenarios.append(('single_row', False, str(e)))
        
        # Evaluate error handling
        successful_scenarios = sum(1 for _, success, _ in error_scenarios if success)
        total_scenarios = len(error_scenarios)
        
        print(f"✅ Batch Error Handling:")
        for scenario_name, success, result in error_scenarios:
            status = "✅ Success" if success else "❌ Failed"
            print(f"   {scenario_name}: {status} - {result}")
        
        print(f"   Overall success rate: {successful_scenarios}/{total_scenarios}")
        
        # At least most scenarios should be handled gracefully
        self.assertGreaterEqual(successful_scenarios / total_scenarios, 0.75)
    
    def test_large_scale_batch_processing(self):
        """Test processing of very large batches"""
        print("🔄 Testing large-scale batch processing...")
        
        large_batch_sizes = [5000, 10000]
        
        for batch_size in large_batch_sizes:
            print(f"   Processing {batch_size:,} samples...")
            
            # Generate large batch (in chunks to avoid memory issues)
            chunk_size = 1000
            chunks = []
            
            for i in range(0, batch_size, chunk_size):
                current_chunk_size = min(chunk_size, batch_size - i)
                chunk = self.quality_model.generate_synthetic_training_data(n_samples=current_chunk_size)
                chunks.append(chunk)
            
            large_batch = pd.concat(chunks, ignore_index=True)
            
            # Process large batch
            start_time = time.time()
            results = self.quality_model.batch_predict(large_batch)
            processing_time = time.time() - start_time
            
            throughput = batch_size / processing_time
            memory_usage = psutil.Process().memory_info().rss / 1024 / 1024  # MB
            
            # Validate large batch processing
            self.assertEqual(len(results), batch_size)
            self.assertLess(processing_time, batch_size * 0.01,  # Less than 10ms per sample
                           f"Large batch processing too slow: {processing_time:.2f}s")
            
            print(f"     ✅ {batch_size:,} samples: {throughput:.0f} pred/sec, {memory_usage:.0f}MB RAM")
            
            # Clean up
            del large_batch
            del results
            import gc
            gc.collect()


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)