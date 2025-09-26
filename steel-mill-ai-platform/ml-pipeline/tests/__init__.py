"""
Steel Mill AI Platform - Test Suite
Phase 1: ML Core Functionality Testing
"""

# Test configuration
TEST_CONFIG = {
    'models': {
        'quality_prediction': {
            'accuracy_target': 0.90,
            'r2_target': 0.85,
            'response_time_ms': 200
        },
        'defect_detection': {
            'accuracy_target': 0.85,
            'confidence_threshold': 0.7,
            'response_time_ms': 200
        },
        'predictive_maintenance': {
            'false_positive_rate': 0.15,
            'risk_threshold': 0.6,
            'response_time_ms': 200
        }
    },
    'api': {
        'base_url': 'http://localhost:8000',
        'timeout': 10,
        'max_concurrent_requests': 100
    },
    'data': {
        'synthetic_samples': 10000,
        'test_samples': 1000,
        'batch_size': 100
    }
}

# Test data paths
TEST_DATA_DIR = './tests/data'
TEST_RESULTS_DIR = './tests/results'
TEST_MODELS_DIR = './tests/models'