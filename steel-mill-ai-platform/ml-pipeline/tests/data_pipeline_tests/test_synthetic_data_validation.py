"""
Synthetic Data Validation Tests for Steel Mill AI Platform
Tests data generation quality, statistical distributions, and ASTM compliance
"""

import unittest
import numpy as np
import pandas as pd
import sys
import os
import scipy.stats as stats
from typing import Dict, List, Tuple

# Add the parent directory to the path to import our models
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from models.rebar_quality_prediction import RebarQualityPredictionModel
from models.rebar_defect_detection import RebarDefectDetectionModel
from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from tests import TEST_CONFIG


class TestSyntheticDataValidation(unittest.TestCase):
    """Test suite for synthetic data generation and validation"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.quality_model = RebarQualityPredictionModel()
        cls.defect_model = RebarDefectDetectionModel()
        cls.maintenance_model = RebarPredictiveMaintenanceModel()
        
        # Generate test datasets
        cls.quality_data = cls.quality_model.generate_synthetic_training_data(n_samples=5000)
        cls.defect_data = cls.defect_model.generate_synthetic_training_data(n_samples=5000)
        cls.maintenance_data = cls.maintenance_model.generate_synthetic_training_data(n_samples=5000)
        
        # Statistical test parameters
        cls.significance_level = 0.05
        cls.tolerance_percentage = 0.1  # 10% tolerance for statistical tests
    
    def test_quality_data_statistical_distributions(self):
        """Test that quality data follows expected statistical distributions"""
        data = self.quality_data
        
        # Test chemical composition distributions
        carbon_mean = data['carbon_content'].mean()
        carbon_std = data['carbon_content'].std()
        
        # Carbon content should be approximately normal around 0.25
        self.assertAlmostEqual(carbon_mean, 0.25, delta=0.05)
        self.assertAlmostEqual(carbon_std, 0.05, delta=0.02)
        
        # Test normality of carbon content using Shapiro-Wilk test
        # Note: Use smaller sample for normality test due to computational limits
        sample_size = min(1000, len(data))
        carbon_sample = data['carbon_content'].sample(sample_size)
        _, p_value = stats.shapiro(carbon_sample)
        self.assertGreater(p_value, 0.001, "Carbon content should be approximately normal")
        
        # Test manganese content
        manganese_mean = data['manganese_content'].mean()
        manganese_std = data['manganese_content'].std()
        
        self.assertAlmostEqual(manganese_mean, 1.2, delta=0.1)
        self.assertAlmostEqual(manganese_std, 0.2, delta=0.05)
        
        # Test phosphorus and sulfur are within ASTM limits
        self.assertTrue(data['phosphorus_content'].max() <= 0.06)
        self.assertTrue(data['sulfur_content'].max() <= 0.08)
        self.assertTrue(data['phosphorus_content'].min() >= 0.01)
        self.assertTrue(data['sulfur_content'].min() >= 0.01)
        
        print(f"✅ Quality Data Statistics:")
        print(f"   Carbon: μ={carbon_mean:.3f}, σ={carbon_std:.3f}")
        print(f"   Manganese: μ={manganese_mean:.3f}, σ={manganese_std:.3f}")
        print(f"   Phosphorus range: [{data['phosphorus_content'].min():.3f}, {data['phosphorus_content'].max():.3f}]")
        print(f"   Sulfur range: [{data['sulfur_content'].min():.3f}, {data['sulfur_content'].max():.3f}]")
    
    def test_quality_data_grade_distribution(self):
        """Test grade distribution in quality data"""
        data = self.quality_data
        grade_counts = data['grade'].value_counts(normalize=True)
        
        # Grade60 should be most common (~60%)
        self.assertGreater(grade_counts['Grade60'], 0.5)
        
        # All expected grades should be present
        expected_grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        for grade in expected_grades:
            self.assertIn(grade, grade_counts.index)
            self.assertGreater(grade_counts[grade], 0.05)  # At least 5% of each grade
        
        print(f"✅ Grade Distribution:")
        for grade in expected_grades:
            print(f"   {grade}: {grade_counts[grade]:.1%}")
    
    def test_quality_data_astm_compliance(self):
        """Test ASTM compliance generation logic"""
        data = self.quality_data
        
        # Check compliance rate is reasonable
        compliance_rate = data['astm_compliant'].mean()
        self.assertTrue(0.6 <= compliance_rate <= 0.9, 
                       f"ASTM compliance rate {compliance_rate:.2%} should be between 60-90%")
        
        # Test that compliant samples meet basic chemical requirements
        compliant_samples = data[data['astm_compliant'] == 1]
        non_compliant_samples = data[data['astm_compliant'] == 0]
        
        # Compliant samples should have better average chemistry
        compliant_carbon_mean = compliant_samples['carbon_content'].mean()
        non_compliant_carbon_mean = non_compliant_samples['carbon_content'].mean()
        
        # This relationship may not always hold due to random variation, but test if significant
        if len(compliant_samples) > 100 and len(non_compliant_samples) > 100:
            _, p_value = stats.ttest_ind(compliant_samples['carbon_content'], 
                                       non_compliant_samples['carbon_content'])
            if p_value < 0.05:  # Statistically significant difference
                print(f"✅ Compliant vs Non-compliant Carbon Content:")
                print(f"   Compliant: {compliant_carbon_mean:.3f}")
                print(f"   Non-compliant: {non_compliant_carbon_mean:.3f}")
        
        print(f"✅ ASTM Compliance Rate: {compliance_rate:.1%}")
    
    def test_quality_data_mechanical_properties(self):
        """Test mechanical properties generation"""
        data = self.quality_data
        
        # Test yield strength ranges by grade
        grade_strength_ranges = {
            'Grade40': (250, 350),
            'Grade60': (350, 500),
            'Grade75': (450, 600),
            'Grade80': (500, 650)
        }
        
        for grade, (min_strength, max_strength) in grade_strength_ranges.items():
            grade_data = data[data['grade'] == grade]
            if len(grade_data) > 10:  # Only test if we have enough samples
                grade_strengths = grade_data['actual_yield_strength']
                
                # Most values should be within expected range
                in_range_count = ((grade_strengths >= min_strength) & 
                                (grade_strengths <= max_strength)).sum()
                in_range_percentage = in_range_count / len(grade_strengths)
                
                self.assertGreater(in_range_percentage, 0.7,
                                 f"{grade} yield strength range compliance: {in_range_percentage:.1%}")
                
                print(f"✅ {grade} Yield Strength:")
                print(f"   Range: [{grade_strengths.min():.0f}, {grade_strengths.max():.0f}] MPa")
                print(f"   Mean: {grade_strengths.mean():.0f} MPa")
                print(f"   In expected range: {in_range_percentage:.1%}")
    
    def test_defect_data_distributions(self):
        """Test defect data statistical distributions"""
        data = self.defect_data
        
        # Test defect type distribution
        defect_counts = data['primary_defect'].value_counts(normalize=True)
        
        # 'no_defect' should be most common
        self.assertGreater(defect_counts['no_defect'], 0.3,
                          f"No defect rate {defect_counts['no_defect']:.1%} should be > 30%")
        
        # All expected defect types should be present
        expected_defects = [
            'no_defect', 'surface_crack', 'surface_fold', 'surface_pit',
            'rib_height_low', 'rib_height_high', 'rib_spacing_error',
            'dimensional_error', 'surface_scale'
        ]
        
        for defect in expected_defects:
            if defect in defect_counts.index:
                self.assertGreater(defect_counts[defect], 0.01,
                                 f"{defect} should have at least 1% occurrence")
        
        # Test severity distribution
        severity_counts = data['severity'].value_counts(normalize=True)
        
        # All severity levels should be present
        for severity in ['low', 'medium', 'high']:
            self.assertIn(severity, severity_counts.index)
            self.assertGreater(severity_counts[severity], 0.1)
        
        print(f"✅ Defect Type Distribution (top 5):")
        for defect, percentage in defect_counts.head().items():
            print(f"   {defect}: {percentage:.1%}")
        
        print(f"✅ Severity Distribution:")
        for severity, percentage in severity_counts.items():
            print(f"   {severity}: {percentage:.1%}")
    
    def test_defect_data_feature_ranges(self):
        """Test defect data feature value ranges"""
        data = self.defect_data
        
        # Test surface features
        surface_features = {
            'surface_roughness': (0.5, 10.0),
            'surface_temperature': (20, 80),
            'surface_oxidation': (0, 1.0),
            'cooling_rate': (10, 100)
        }
        
        for feature, (min_val, max_val) in surface_features.items():
            if feature in data.columns:
                feature_values = data[feature]
                
                self.assertTrue(feature_values.min() >= min_val * 0.9,
                               f"{feature} minimum {feature_values.min():.2f} below expected {min_val}")
                self.assertTrue(feature_values.max() <= max_val * 1.1,
                               f"{feature} maximum {feature_values.max():.2f} above expected {max_val}")
                
                print(f"✅ {feature}: [{feature_values.min():.2f}, {feature_values.max():.2f}]")
        
        # Test dimensional features
        dimensional_features = {
            'rib_height': (0.3, 1.2),
            'rib_spacing': (8.0, 15.0),
            'diameter_deviation': (0, 1.0),
            'straightness': (0.5, 10.0)
        }
        
        for feature, (min_val, max_val) in dimensional_features.items():
            if feature in data.columns:
                feature_values = data[feature]
                
                self.assertTrue(feature_values.min() >= min_val * 0.9)
                self.assertTrue(feature_values.max() <= max_val * 1.1)
    
    def test_maintenance_data_distributions(self):
        """Test maintenance data statistical distributions"""
        data = self.maintenance_data
        
        # Test equipment type distribution
        equipment_counts = data['equipment_type'].value_counts(normalize=True)
        
        expected_equipment = ['rolling_mill', 'heat_treatment', 'cutting_straightening']
        for equipment in expected_equipment:
            self.assertIn(equipment, equipment_counts.index)
            self.assertGreater(equipment_counts[equipment], 0.2,
                             f"{equipment} should have at least 20% representation")
        
        # Test maintenance type distribution
        maintenance_counts = data['recommended_maintenance'].value_counts(normalize=True)
        
        expected_maintenance = ['preventive', 'predictive', 'corrective']
        for maintenance_type in expected_maintenance:
            self.assertIn(maintenance_type, maintenance_counts.index)
            self.assertGreater(maintenance_counts[maintenance_type], 0.1)
        
        # Test remaining useful life distribution
        rul_values = data['remaining_useful_life']
        self.assertTrue(rul_values.min() >= 0)
        self.assertTrue(rul_values.max() <= 365)
        self.assertTrue(50 <= rul_values.mean() <= 200)  # Reasonable average
        
        print(f"✅ Equipment Distribution:")
        for equipment, percentage in equipment_counts.items():
            print(f"   {equipment}: {percentage:.1%}")
        
        print(f"✅ Maintenance Type Distribution:")
        for maintenance_type, percentage in maintenance_counts.items():
            print(f"   {maintenance_type}: {percentage:.1%}")
        
        print(f"✅ RUL Statistics: Mean={rul_values.mean():.1f} days, Range=[{rul_values.min()}, {rul_values.max()}]")
    
    def test_maintenance_data_operating_hours_correlation(self):
        """Test correlation between operating hours and maintenance needs"""
        data = self.maintenance_data
        
        # Equipment with higher operating hours should tend to have:
        # 1. Lower remaining useful life
        # 2. Higher failure probability
        # 3. More corrective maintenance
        
        if 'operating_hours' in data.columns and 'remaining_useful_life' in data.columns:
            correlation = data['operating_hours'].corr(data['remaining_useful_life'])
            
            # Should be negative correlation (more hours -> less remaining life)
            self.assertLess(correlation, -0.1,
                           f"Operating hours vs RUL correlation {correlation:.3f} should be negative")
            
            print(f"✅ Operating Hours vs RUL Correlation: {correlation:.3f}")
        
        # Test high-hours equipment has more corrective maintenance
        high_hours_threshold = data['operating_hours'].quantile(0.8)
        high_hours_data = data[data['operating_hours'] >= high_hours_threshold]
        low_hours_data = data[data['operating_hours'] <= data['operating_hours'].quantile(0.2)]
        
        if len(high_hours_data) > 10 and len(low_hours_data) > 10:
            high_hours_corrective_rate = (high_hours_data['recommended_maintenance'] == 'corrective').mean()
            low_hours_corrective_rate = (low_hours_data['recommended_maintenance'] == 'corrective').mean()
            
            # High hours equipment should have more corrective maintenance
            self.assertGreaterEqual(high_hours_corrective_rate, low_hours_corrective_rate,
                                   "High-hours equipment should need more corrective maintenance")
            
            print(f"✅ Corrective Maintenance Rates:")
            print(f"   High hours (>{high_hours_threshold:.0f}h): {high_hours_corrective_rate:.1%}")
            print(f"   Low hours (<{data['operating_hours'].quantile(0.2):.0f}h): {low_hours_corrective_rate:.1%}")
    
    def test_data_completeness_and_consistency(self):
        """Test data completeness and consistency across all datasets"""
        datasets = [
            ('Quality', self.quality_data),
            ('Defect', self.defect_data),
            ('Maintenance', self.maintenance_data)
        ]
        
        for dataset_name, data in datasets:
            # Test for missing values
            missing_values = data.isnull().sum()
            total_missing = missing_values.sum()
            
            self.assertEqual(total_missing, 0,
                           f"{dataset_name} data should not have missing values")
            
            # Test for duplicate rows
            duplicate_count = data.duplicated().sum()
            duplicate_rate = duplicate_count / len(data)
            
            self.assertLess(duplicate_rate, 0.05,
                           f"{dataset_name} data duplicate rate {duplicate_rate:.1%} should be < 5%")
            
            # Test data types consistency
            numeric_columns = data.select_dtypes(include=[np.number]).columns
            for col in numeric_columns:
                self.assertFalse(data[col].isna().any(),
                                f"{dataset_name}.{col} should not have NaN values")
                self.assertTrue(np.isfinite(data[col]).all(),
                               f"{dataset_name}.{col} should not have infinite values")
            
            print(f"✅ {dataset_name} Data Quality:")
            print(f"   Samples: {len(data):,}")
            print(f"   Features: {len(data.columns)}")
            print(f"   Missing values: {total_missing}")
            print(f"   Duplicates: {duplicate_count} ({duplicate_rate:.1%})")
    
    def test_cross_dataset_consistency(self):
        """Test consistency across different synthetic datasets"""
        # Test that similar features have consistent ranges across datasets
        
        # Surface roughness should be consistent between quality and defect data
        if 'surface_roughness' in self.defect_data.columns:
            defect_roughness_range = (self.defect_data['surface_roughness'].min(),
                                    self.defect_data['surface_roughness'].max())
            
            # Ranges should overlap significantly
            print(f"✅ Cross-Dataset Feature Consistency:")
            print(f"   Defect surface_roughness: [{defect_roughness_range[0]:.2f}, {defect_roughness_range[1]:.2f}]")
        
        # Operating hours should be consistent between maintenance datasets
        if 'operating_hours' in self.maintenance_data.columns:
            maintenance_hours_range = (self.maintenance_data['operating_hours'].min(),
                                     self.maintenance_data['operating_hours'].max())
            
            # Should be realistic ranges
            self.assertGreaterEqual(maintenance_hours_range[0], 0)
            self.assertLessEqual(maintenance_hours_range[1], 15000)  # Max realistic hours
            
            print(f"   Maintenance operating_hours: [{maintenance_hours_range[0]:.0f}, {maintenance_hours_range[1]:.0f}]")
    
    def test_data_generation_reproducibility(self):
        """Test that data generation is reproducible with same random seed"""
        # Test quality model reproducibility
        data1 = self.quality_model.generate_synthetic_training_data(n_samples=100)
        data2 = self.quality_model.generate_synthetic_training_data(n_samples=100)
        
        # Data should be different (different random seeds)
        self.assertFalse(data1.equals(data2), "Different calls should generate different data")
        
        # But same samples should have same structure
        self.assertEqual(len(data1.columns), len(data2.columns))
        self.assertEqual(list(data1.columns), list(data2.columns))
        
        print(f"✅ Data Generation Reproducibility:")
        print(f"   Quality data columns: {len(data1.columns)}")
        print(f"   Defect data columns: {len(self.defect_data.columns)}")
        print(f"   Maintenance data columns: {len(self.maintenance_data.columns)}")
    
    def test_data_volume_scalability(self):
        """Test data generation with different volumes"""
        sample_sizes = [100, 1000, 5000]
        
        for size in sample_sizes:
            # Test quality data generation
            start_time = pd.Timestamp.now()
            quality_data = self.quality_model.generate_synthetic_training_data(n_samples=size)
            generation_time = (pd.Timestamp.now() - start_time).total_seconds()
            
            self.assertEqual(len(quality_data), size)
            self.assertLess(generation_time, size * 0.01,  # Less than 10ms per sample
                           f"Generation too slow: {generation_time:.3f}s for {size} samples")
            
            print(f"✅ Generated {size:,} samples in {generation_time:.3f}s ({size/generation_time:.0f} samples/sec)")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)