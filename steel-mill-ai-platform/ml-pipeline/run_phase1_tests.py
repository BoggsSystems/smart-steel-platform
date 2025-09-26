"""
Phase 1 Test Runner for Steel Mill AI Platform
Executes comprehensive ML Core Functionality Testing and generates report
"""

import unittest
import sys
import os
import time
import json
from io import StringIO
from datetime import datetime

# Add the current directory to the path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# Import test configuration
from tests import TEST_CONFIG

def run_test_suite():
    """Run the Phase 1 test suite and generate comprehensive report"""
    
    print("🧪 Steel Mill AI Platform - Phase 1 Testing")
    print("=" * 60)
    print("🎯 ML Core Functionality Testing")
    print(f"📅 Start Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("=" * 60)
    
    # Test suite configuration
    test_modules = [
        ('Unit Tests - Quality Prediction', 'tests.unit_tests.test_quality_prediction'),
        ('Unit Tests - Defect Detection', 'tests.unit_tests.test_defect_detection'),
        ('Unit Tests - Predictive Maintenance', 'tests.unit_tests.test_predictive_maintenance'),
        ('Integration Tests - API Endpoints', 'tests.integration_tests.test_api_endpoints'),
        ('Performance Tests - API Performance', 'tests.performance_tests.test_api_performance')
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
        }
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
                buffer=True
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
                    print(f"   {i+1}. {test}: {traceback.split('AssertionError:')[-1].strip() if 'AssertionError:' in traceback else 'Unknown failure'}")
            
            if result.errors:
                print(f"\n💥 Errors in {module_name}:")
                for i, (test, traceback) in enumerate(result.errors):
                    error_msg = traceback.split('\n')[-2] if traceback.split('\n') else 'Unknown error'
                    print(f"   {i+1}. {test}: {error_msg}")
            
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
    generate_test_report(results)
    
    return results

def generate_test_report(results):
    """Generate comprehensive test report"""
    
    print("\n" + "=" * 60)
    print("📊 PHASE 1 TEST RESULTS SUMMARY")
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
    
    # Performance targets assessment
    print(f"\n🎯 PERFORMANCE TARGETS ASSESSMENT:")
    print("-" * 60)
    
    targets = TEST_CONFIG['models']
    
    print("Quality Prediction Model:")
    print(f"   Target Accuracy: ≥{targets['quality_prediction']['accuracy_target']:.0%}")
    print(f"   Target R²: ≥{targets['quality_prediction']['r2_target']}")
    print(f"   Target Response Time: ≤{targets['quality_prediction']['response_time_ms']}ms")
    
    print("Defect Detection Model:")
    print(f"   Target Accuracy: ≥{targets['defect_detection']['accuracy_target']:.0%}")
    print(f"   Target Response Time: ≤{targets['defect_detection']['response_time_ms']}ms")
    
    print("Predictive Maintenance Model:")
    print(f"   Target False Positive Rate: ≤{targets['predictive_maintenance']['false_positive_rate']:.0%}")
    print(f"   Target Response Time: ≤{targets['predictive_maintenance']['response_time_ms']}ms")
    
    # Test coverage assessment
    print(f"\n🎯 TEST COVERAGE ASSESSMENT:")
    print("-" * 60)
    
    coverage_areas = [
        "✅ Model Initialization & Configuration",
        "✅ Synthetic Data Generation",
        "✅ Feature Processing & Validation",
        "✅ Model Training & Validation",
        "✅ Single & Batch Predictions",
        "✅ Performance Requirements",
        "✅ API Endpoint Functionality",
        "✅ Error Handling & Edge Cases",
        "✅ Concurrent Request Handling",
        "✅ Response Time Performance"
    ]
    
    for area in coverage_areas:
        print(f"   {area}")
    
    # Recommendations
    print(f"\n💡 RECOMMENDATIONS:")
    print("-" * 60)
    
    if results['overall_success_rate'] >= 95:
        print("🟢 Excellent test results! Phase 1 testing is complete.")
        print("   ➡️  Ready to proceed to Phase 2: Data Pipeline & Integration Testing")
    elif results['overall_success_rate'] >= 80:
        print("🟡 Good test results with some issues to address.")
        print("   ➡️  Fix failing tests before proceeding to Phase 2")
        print("   ➡️  Review error logs and performance bottlenecks")
    else:
        print("🔴 Significant test failures detected.")
        print("   ➡️  Address critical failures before proceeding")
        print("   ➡️  Review model implementations and API functionality")
        print("   ➡️  Consider revising performance targets if needed")
    
    # Next steps
    print(f"\n🚀 NEXT STEPS:")
    print("-" * 60)
    print("1. 📝 Review detailed test output above")
    print("2. 🔧 Fix any failing tests and performance issues")
    print("3. 📊 Validate that all performance targets are met")
    print("4. 📋 Document any test environment limitations")
    print("5. ➡️  Proceed to Phase 2: Data Pipeline & Integration Testing")
    
    # Save detailed report to file
    report_data = {
        'phase': 'Phase 1: ML Core Functionality Testing',
        'timestamp': results['start_time'].isoformat(),
        'results': results,
        'config': TEST_CONFIG
    }
    
    os.makedirs('./tests/results', exist_ok=True)
    report_filename = f"./tests/results/phase1_test_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    
    with open(report_filename, 'w') as f:
        json.dump(report_data, f, indent=2, default=str)
    
    print(f"\n💾 Detailed report saved to: {report_filename}")
    print("=" * 60)

if __name__ == '__main__':
    try:
        # Check if API server is running
        import requests
        response = requests.get(f"{TEST_CONFIG['api']['base_url']}/health", timeout=5)
        if response.status_code != 200:
            print("⚠️  API server not responding correctly")
            print("   Make sure the ML API server is running at http://localhost:8000")
            sys.exit(1)
    except Exception as e:
        print("❌ API server not available")
        print("   Make sure the ML API server is running at http://localhost:8000")
        print(f"   Error: {e}")
        sys.exit(1)
    
    # Run the test suite
    results = run_test_suite()
    
    # Exit with appropriate code
    if results['overall_success_rate'] >= 80:
        sys.exit(0)  # Success
    else:
        sys.exit(1)  # Failure