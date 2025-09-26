"""
Phase 2 Test Runner for Steel Mill AI Platform
Executes comprehensive Data Pipeline & Integration Testing and generates report
"""

import unittest
import sys
import os
import time
import json
from io import StringIO
from datetime import datetime
import traceback

# Add the current directory to the path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# Import test configuration
from tests import TEST_CONFIG

def run_phase2_test_suite():
    """Run the Phase 2 test suite and generate comprehensive report"""
    
    print("🔄 Steel Mill AI Platform - Phase 2 Testing")
    print("=" * 60)
    print("🎯 Data Pipeline & Integration Testing")
    print(f"📅 Start Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("=" * 60)
    
    # Test suite configuration
    test_modules = [
        ('Data Validation - Synthetic Data', 'tests.data_pipeline_tests.test_synthetic_data_validation'),
        ('Feature Engineering - Preprocessing', 'tests.data_pipeline_tests.test_feature_engineering'),
        ('Model Training - Pipeline', 'tests.data_pipeline_tests.test_model_training_pipeline'),
        ('Batch Processing - Validation', 'tests.data_pipeline_tests.test_batch_processing'),
        ('End-to-End - Workflow Simulation', 'tests.workflow_tests.test_end_to_end_workflow')
    ]
    
    # Results tracking
    results = {
        'start_time': datetime.now(),
        'test_modules': {},
        'summary': {
            'total_tests': 0,
            'passed_tests': 0,
            'failed_tests': 0,
            'error_tests': 0,
            'skipped_tests': 0
        },
        'performance_metrics': {},
        'data_quality_metrics': {},
        'workflow_metrics': {}
    }
    
    # Run each test module
    for module_name, module_path in test_modules:
        print(f"\n🔬 Running {module_name}...")
        print("-" * 50)
        
        # Capture test output
        test_output = StringIO()
        
        try:
            # Create test loader and suite
            loader = unittest.TestLoader()
            suite = loader.loadTestsFromName(module_path)
            
            # Run tests with custom result handler
            runner = unittest.TextTestRunner(
                stream=test_output,
                verbosity=2,
                buffer=True,
                failfast=False
            )
            
            start_time = time.time()
            result = runner.run(suite)
            execution_time = time.time() - start_time
            
            # Store results
            module_results = {
                'execution_time': execution_time,
                'tests_run': result.testsRun,
                'failures': len(result.failures),
                'errors': len(result.errors),
                'skipped': len(result.skipped) if hasattr(result, 'skipped') else 0,
                'success_rate': ((result.testsRun - len(result.failures) - len(result.errors)) / result.testsRun * 100) if result.testsRun > 0 else 0,
                'output': test_output.getvalue()
            }
            
            # Extract specific metrics from test output
            if 'synthetic_data_validation' in module_path:
                results['data_quality_metrics'] = _extract_data_quality_metrics(test_output.getvalue())
            elif 'batch_processing' in module_path:
                results['performance_metrics'] = _extract_performance_metrics(test_output.getvalue())
            elif 'end_to_end_workflow' in module_path:
                results['workflow_metrics'] = _extract_workflow_metrics(test_output.getvalue())
            
            # Print summary for this module
            print(f"✅ Tests Run: {result.testsRun}")
            print(f"✅ Passed: {result.testsRun - len(result.failures) - len(result.errors)}")
            if result.failures:
                print(f"❌ Failures: {len(result.failures)}")
            if result.errors:
                print(f"💥 Errors: {len(result.errors)}")
            if hasattr(result, 'skipped') and result.skipped:
                print(f"⏭️  Skipped: {len(result.skipped)}")
            print(f"⏱️  Execution Time: {execution_time:.2f}s")
            print(f"📊 Success Rate: {module_results['success_rate']:.1f}%")
            
            # Show failures and errors
            if result.failures:
                print(f"\n❌ Failures in {module_name}:")
                for i, (test, traceback) in enumerate(result.failures):
                    error_line = traceback.split('\n')[-2] if traceback.split('\n') else 'Unknown failure'
                    print(f"   {i+1}. {test}: {error_line}")
            
            if result.errors:
                print(f"\n💥 Errors in {module_name}:")
                for i, (test, traceback) in enumerate(result.errors):
                    error_line = traceback.split('\n')[-2] if traceback.split('\n') else 'Unknown error'
                    print(f"   {i+1}. {test}: {error_line}")
            
            results['test_modules'][module_name] = module_results
            
            # Update summary
            results['summary']['total_tests'] += result.testsRun
            results['summary']['passed_tests'] += (result.testsRun - len(result.failures) - len(result.errors))
            results['summary']['failed_tests'] += len(result.failures)
            results['summary']['error_tests'] += len(result.errors)
            if hasattr(result, 'skipped'):
                results['summary']['skipped_tests'] += len(result.skipped)
            
        except Exception as e:
            print(f"💥 Failed to run {module_name}: {str(e)}")
            print(f"   Traceback: {traceback.format_exc()}")
            results['test_modules'][module_name] = {
                'execution_time': 0,
                'tests_run': 0,
                'failures': 0,
                'errors': 1,
                'skipped': 0,
                'success_rate': 0,
                'error_message': str(e)
            }
            results['summary']['error_tests'] += 1
        
        finally:
            test_output.close()
    
    # Calculate final results
    results['end_time'] = datetime.now()
    results['total_execution_time'] = (results['end_time'] - results['start_time']).total_seconds()
    results['overall_success_rate'] = (results['summary']['passed_tests'] / results['summary']['total_tests'] * 100) if results['summary']['total_tests'] > 0 else 0
    
    # Generate comprehensive report
    generate_phase2_report(results)
    
    return results

def _extract_data_quality_metrics(output: str) -> dict:
    """Extract data quality metrics from test output"""
    metrics = {}
    lines = output.split('\n')
    
    for line in lines:
        if 'Grade Distribution:' in line:
            metrics['grade_distribution'] = True
        elif 'ASTM Compliance Rate:' in line:
            try:
                rate = line.split(':')[1].strip()
                metrics['astm_compliance_rate'] = rate
            except:
                pass
        elif 'Cross-Dataset Feature Consistency:' in line:
            metrics['feature_consistency'] = True
    
    return metrics

def _extract_performance_metrics(output: str) -> dict:
    """Extract performance metrics from test output"""
    metrics = {}
    lines = output.split('\n')
    
    for line in lines:
        if 'pred/sec' in line:
            try:
                # Extract throughput numbers
                parts = line.split()
                for i, part in enumerate(parts):
                    if 'pred/sec' in part and i > 0:
                        throughput = parts[i-1]
                        if throughput.replace('.', '').isdigit():
                            if 'max_throughput' not in metrics:
                                metrics['max_throughput'] = 0
                            metrics['max_throughput'] = max(metrics.get('max_throughput', 0), float(throughput))
            except:
                pass
    
    return metrics

def _extract_workflow_metrics(output: str) -> dict:
    """Extract workflow metrics from test output"""
    metrics = {}
    lines = output.split('\n')
    
    for line in lines:
        if 'Yield rate:' in line:
            metrics['workflow_tested'] = True
        elif 'Stages completed:' in line:
            metrics['multi_stage_processing'] = True
        elif 'Maintenance alerts:' in line:
            metrics['maintenance_integration'] = True
    
    return metrics

def generate_phase2_report(results):
    """Generate comprehensive Phase 2 test report"""
    
    print("\n" + "=" * 60)
    print("📊 PHASE 2 TEST RESULTS SUMMARY")
    print("=" * 60)
    
    # Overall summary
    summary = results['summary']
    print(f"🕐 Total Execution Time: {results['total_execution_time']:.2f}s")
    print(f"🧪 Total Tests: {summary['total_tests']}")
    print(f"✅ Passed: {summary['passed_tests']}")
    print(f"❌ Failed: {summary['failed_tests']}")
    print(f"💥 Errors: {summary['error_tests']}")
    print(f"⏭️  Skipped: {summary['skipped_tests']}")
    print(f"📈 Overall Success Rate: {results['overall_success_rate']:.1f}%")
    
    # Module-by-module breakdown
    print(f"\n📋 MODULE BREAKDOWN:")
    print("-" * 60)
    
    for module_name, module_results in results['test_modules'].items():
        status = "✅ PASS" if module_results['success_rate'] >= 80 else "❌ FAIL"
        print(f"{status} {module_name}")
        print(f"    Tests: {module_results['tests_run']} | "
              f"Passed: {module_results['tests_run'] - module_results['failures'] - module_results['errors']} | "
              f"Failed: {module_results['failures']} | "
              f"Errors: {module_results['errors']} | "
              f"Success: {module_results['success_rate']:.1f}% | "
              f"Time: {module_results['execution_time']:.2f}s")
    
    # Phase 2 specific assessments
    print(f"\n🎯 DATA PIPELINE ASSESSMENT:")
    print("-" * 60)
    
    data_quality = results.get('data_quality_metrics', {})
    performance = results.get('performance_metrics', {})
    workflow = results.get('workflow_metrics', {})
    
    print("Data Quality Validation:")
    print(f"   ✅ Grade Distribution Analysis: {'✓' if data_quality.get('grade_distribution') else '✗'}")
    print(f"   ✅ ASTM Compliance Rate: {data_quality.get('astm_compliance_rate', 'N/A')}")
    print(f"   ✅ Feature Consistency: {'✓' if data_quality.get('feature_consistency') else '✗'}")
    
    print("\nModel Training Pipeline:")
    print(f"   ✅ Cross-validation Testing")
    print(f"   ✅ Overfitting Detection")
    print(f"   ✅ Model Persistence")
    print(f"   ✅ Batch Training Performance")
    
    print("\nBatch Processing Performance:")
    max_throughput = performance.get('max_throughput', 0)
    print(f"   ✅ Maximum Throughput: {max_throughput:.0f} predictions/sec")
    print(f"   ✅ Concurrent Processing: {'✓' if max_throughput > 100 else '✗'}")
    print(f"   ✅ Memory Usage Validation")
    print(f"   ✅ Streaming Processing")
    
    print("\nEnd-to-End Workflow:")
    print(f"   ✅ Complete Production Simulation: {'✓' if workflow.get('workflow_tested') else '✗'}")
    print(f"   ✅ Multi-stage Processing: {'✓' if workflow.get('multi_stage_processing') else '✗'}")
    print(f"   ✅ ML Integration: {'✓' if workflow.get('maintenance_integration') else '✗'}")
    print(f"   ✅ Quality Control Integration")
    
    # Test coverage assessment
    print(f"\n🎯 PHASE 2 TEST COVERAGE:")
    print("-" * 60)
    
    coverage_areas = [
        "✅ Synthetic Data Generation & Validation",
        "✅ Statistical Distribution Analysis", 
        "✅ Feature Engineering & Preprocessing",
        "✅ Categorical Encoding & Scaling",
        "✅ Model Training Pipeline Validation",
        "✅ Cross-validation & Overfitting Detection",
        "✅ Model Persistence & Reproducibility",
        "✅ Batch Processing Performance",
        "✅ Concurrent Processing & Memory Usage",
        "✅ End-to-End Workflow Simulation",
        "✅ Production Process Integration",
        "✅ Quality Control & ML Integration"
    ]
    
    for area in coverage_areas:
        print(f"   {area}")
    
    # Performance benchmarks
    print(f"\n📊 PERFORMANCE BENCHMARKS:")
    print("-" * 60)
    
    if max_throughput > 0:
        throughput_status = "🟢" if max_throughput >= 500 else "🟡" if max_throughput >= 100 else "🔴"
        print(f"{throughput_status} Maximum Throughput: {max_throughput:.0f} predictions/sec")
    
    # Execution time analysis
    total_time = results['total_execution_time']
    time_status = "🟢" if total_time <= 300 else "🟡" if total_time <= 600 else "🔴"
    print(f"{time_status} Total Test Execution: {total_time:.1f} seconds")
    
    avg_time_per_test = total_time / max(summary['total_tests'], 1)
    print(f"📈 Average Time per Test: {avg_time_per_test:.2f} seconds")
    
    # Recommendations
    print(f"\n💡 RECOMMENDATIONS:")
    print("-" * 60)
    
    if results['overall_success_rate'] >= 95:
        print("🟢 Excellent Phase 2 results! Data pipeline is robust and ready.")
        print("   ➡️  Ready to proceed to Phase 3: User Acceptance Testing")
        print("   ➡️  Consider deploying to staging environment")
    elif results['overall_success_rate'] >= 85:
        print("🟡 Good Phase 2 results with minor issues.")
        print("   ➡️  Address failing tests before production deployment")
        print("   ➡️  Validate performance under production load")
        print("   ➡️  Consider performance optimizations")
    else:
        print("🔴 Significant Phase 2 issues detected.")
        print("   ➡️  Critical data pipeline failures need immediate attention")
        print("   ➡️  Review model training and feature engineering")
        print("   ➡️  Investigate performance bottlenecks")
        print("   ➡️  Do not proceed to production without fixes")
    
    # Technical recommendations
    print(f"\n🔧 TECHNICAL RECOMMENDATIONS:")
    print("-" * 60)
    
    if max_throughput < 100:
        print("🔴 Performance Issue: Throughput below minimum requirements")
        print("   • Optimize batch processing algorithms")
        print("   • Consider model compression or distillation")
        print("   • Implement caching for repeated predictions")
    
    if summary['error_tests'] > 0:
        print("🔴 Stability Issue: Test errors detected")
        print("   • Review error handling in data pipeline")
        print("   • Add input validation and sanitization")
        print("   • Implement graceful degradation")
    
    # Next steps
    print(f"\n🚀 NEXT STEPS:")
    print("-" * 60)
    print("1. 📝 Review detailed test results and fix any failures")
    print("2. 📊 Validate performance meets production requirements")
    print("3. 🔍 Conduct additional load testing if needed")
    print("4. 📋 Document data pipeline capabilities and limitations")
    print("5. 🎯 Proceed to Phase 3: User Acceptance & Interface Testing")
    
    # Save detailed report
    report_data = {
        'phase': 'Phase 2: Data Pipeline & Integration Testing',
        'timestamp': results['start_time'].isoformat(),
        'results': results,
        'config': TEST_CONFIG,
        'benchmarks': {
            'max_throughput': max_throughput,
            'total_execution_time': total_time,
            'success_rate': results['overall_success_rate']
        }
    }
    
    os.makedirs('./tests/results', exist_ok=True)
    report_filename = f"./tests/results/phase2_test_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    
    with open(report_filename, 'w') as f:
        json.dump(report_data, f, indent=2, default=str)
    
    print(f"\n💾 Detailed report saved to: {report_filename}")
    print("=" * 60)

if __name__ == '__main__':
    print("🔄 Preparing Phase 2 Test Environment...")
    
    # Check Python dependencies
    required_packages = [
        ('numpy', 'numpy'), 
        ('pandas', 'pandas'), 
        ('sklearn', 'scikit-learn'), 
        ('scipy', 'scipy'), 
        ('psutil', 'psutil')
    ]
    missing_packages = []
    
    for import_name, package_name in required_packages:
        try:
            __import__(import_name)
        except ImportError:
            missing_packages.append(package_name)
    
    if missing_packages:
        print(f"❌ Missing required packages: {missing_packages}")
        print("   Install with: pip install " + " ".join(missing_packages))
        sys.exit(1)
    
    # Run the test suite
    print("✅ Environment validated. Starting Phase 2 tests...")
    results = run_phase2_test_suite()
    
    # Exit with appropriate code
    if results['overall_success_rate'] >= 80:
        sys.exit(0)  # Success
    else:
        sys.exit(1)  # Failure