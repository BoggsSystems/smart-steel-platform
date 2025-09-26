"""
End-to-End Workflow Simulation Tests for Steel Mill AI Platform
Tests complete rebar production process simulation and ML integration
"""

import unittest
import numpy as np
import pandas as pd
import sys
import os
import time
from dataclasses import dataclass
from typing import Dict, List, Tuple, Optional
from enum import Enum

# Add the parent directory to the path to import our models
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../..'))

from models.rebar_quality_prediction import RebarQualityPredictionModel
from models.rebar_defect_detection import RebarDefectDetectionModel
from models.rebar_predictive_maintenance import RebarPredictiveMaintenanceModel
from tests import TEST_CONFIG


class ProductionStage(Enum):
    RAW_MATERIAL = "raw_material"
    EAF_MELTING = "eaf_melting"
    CASTING = "casting"
    REHEATING = "reheating"
    ROLLING = "rolling"
    HEAT_TREATMENT = "heat_treatment"
    CUTTING_STRAIGHTENING = "cutting_straightening"
    QUALITY_CONTROL = "quality_control"
    BUNDLING = "bundling"
    SHIPPING = "shipping"


@dataclass
class ProductionBatch:
    """Represents a production batch through the steel mill"""
    batch_id: str
    grade: str
    target_quantity: float  # tons
    current_stage: ProductionStage
    process_parameters: Dict
    quality_metrics: Dict
    defect_history: List[Dict]
    maintenance_alerts: List[Dict]
    stage_timestamps: Dict[ProductionStage, float]
    current_quantity: float = 0.0
    yield_rate: float = 0.95
    
    def __post_init__(self):
        if not self.stage_timestamps:
            self.stage_timestamps = {}


class RebarProductionSimulator:
    """Simulates complete rebar production workflow"""
    
    def __init__(self):
        # Initialize ML models
        self.quality_model = RebarQualityPredictionModel()
        self.defect_model = RebarDefectDetectionModel()
        self.maintenance_model = RebarPredictiveMaintenanceModel()
        
        # Train models with synthetic data
        self._train_models()
        
        # Production parameters
        self.stage_processing_times = {
            ProductionStage.RAW_MATERIAL: 0.5,  # hours
            ProductionStage.EAF_MELTING: 2.0,
            ProductionStage.CASTING: 1.5,
            ProductionStage.REHEATING: 3.0,
            ProductionStage.ROLLING: 2.5,
            ProductionStage.HEAT_TREATMENT: 4.0,
            ProductionStage.CUTTING_STRAIGHTENING: 1.0,
            ProductionStage.QUALITY_CONTROL: 0.5,
            ProductionStage.BUNDLING: 0.5,
            ProductionStage.SHIPPING: 0.25
        }
        
        # Equipment operating hours (for maintenance simulation)
        self.equipment_hours = {
            'eaf_furnace': 5000,
            'casting_machine': 3000,
            'reheating_furnace': 4500,
            'rolling_mill': 6000,
            'heat_treatment': 2000,
            'cutting_straightening': 3500
        }
    
    def _train_models(self):
        """Train all ML models with synthetic data"""
        print("🤖 Training ML models for production simulation...")
        
        # Train quality model
        quality_data = self.quality_model.generate_synthetic_training_data(n_samples=1000)
        quality_targets = {
            'compliance': 'astm_compliant',
            'yield_strength': 'actual_yield_strength',
            'tensile_strength': 'actual_tensile_strength',
            'elongation': 'actual_elongation'
        }
        self.quality_model.train(quality_data, quality_targets)
        
        # Train defect model
        defect_data = self.defect_model.generate_synthetic_training_data(n_samples=1000)
        self.defect_model.train(defect_data, 'primary_defect')
        
        # Train maintenance model
        maintenance_data = self.maintenance_model.generate_synthetic_training_data(n_samples=1000)
        maintenance_targets = {
            'failure_prediction': 'failure_within_days',
            'rul_estimation': 'remaining_useful_life',
            'maintenance_type': 'recommended_maintenance'
        }
        self.maintenance_model.train(maintenance_data, maintenance_targets)
        
        print("✅ ML models trained successfully")
    
    def create_production_batch(self, batch_id: str, grade: str, quantity: float) -> ProductionBatch:
        """Create a new production batch"""
        batch = ProductionBatch(
            batch_id=batch_id,
            grade=grade,
            target_quantity=quantity,
            current_stage=ProductionStage.RAW_MATERIAL,
            process_parameters={},
            quality_metrics={},
            defect_history=[],
            maintenance_alerts=[],
            stage_timestamps={}
        )
        
        batch.stage_timestamps[ProductionStage.RAW_MATERIAL] = time.time()
        
        return batch
    
    def process_raw_materials(self, batch: ProductionBatch) -> Dict:
        """Simulate raw material processing"""
        # Simulate scrap composition analysis
        scrap_composition = {
            'carbon_content': np.random.normal(0.25, 0.05),
            'manganese_content': np.random.normal(1.2, 0.2),
            'phosphorus_content': np.random.normal(0.03, 0.01),
            'sulfur_content': np.random.normal(0.04, 0.01),
            'silicon_content': np.random.normal(0.3, 0.1)
        }
        
        # Store composition in batch parameters
        batch.process_parameters['scrap_composition'] = scrap_composition
        
        return {
            'status': 'completed',
            'composition': scrap_composition,
            'quality_grade': 'acceptable' if scrap_composition['carbon_content'] < 0.35 else 'marginal'
        }
    
    def process_eaf_melting(self, batch: ProductionBatch) -> Dict:
        """Simulate EAF melting process"""
        # Check equipment maintenance
        maintenance_data = {
            'equipment_type': 'heat_treatment',  # Using heat_treatment as proxy for EAF
            'operating_hours': self.equipment_hours['eaf_furnace'],
            'furnace_temperature': np.random.normal(1600, 50),
            'electrode_position': np.random.uniform(0.8, 1.2),
            'power_consumption': np.random.normal(450, 50)
        }
        
        maintenance_prediction = self.maintenance_model.predict_maintenance(maintenance_data)
        
        if maintenance_prediction['risk_level'] in ['high', 'critical']:
            batch.maintenance_alerts.append({
                'equipment': 'eaf_furnace',
                'alert': maintenance_prediction,
                'stage': ProductionStage.EAF_MELTING
            })
        
        # Simulate melting parameters
        melting_params = {
            'tap_temperature': np.random.normal(1650, 25),
            'power_consumption': maintenance_data['power_consumption'],
            'tap_time': np.random.normal(45, 5),  # minutes
            'steel_yield': np.random.normal(0.92, 0.02)
        }
        
        batch.process_parameters['melting'] = melting_params
        batch.current_quantity = batch.target_quantity * melting_params['steel_yield']
        
        return {
            'status': 'completed',
            'parameters': melting_params,
            'maintenance_risk': maintenance_prediction['risk_level'],
            'quantity_produced': batch.current_quantity
        }
    
    def process_casting(self, batch: ProductionBatch) -> Dict:
        """Simulate continuous casting process"""
        casting_params = {
            'casting_temperature': np.random.normal(1520, 30),
            'casting_speed': np.random.normal(1.2, 0.2),
            'water_flow_rate': np.random.normal(150, 10),
            'billet_size': '150x150'  # mm
        }
        
        batch.process_parameters['casting'] = casting_params
        
        # Simulate casting yield loss
        casting_yield = np.random.normal(0.98, 0.01)
        batch.current_quantity *= casting_yield
        
        return {
            'status': 'completed',
            'parameters': casting_params,
            'yield': casting_yield,
            'quantity_after_casting': batch.current_quantity
        }
    
    def process_rolling(self, batch: ProductionBatch) -> Dict:
        """Simulate rolling mill process with ML predictions"""
        # Check rolling mill maintenance
        maintenance_data = {
            'equipment_type': 'rolling_mill',
            'operating_hours': self.equipment_hours['rolling_mill'],
            'vibration_level': np.random.normal(5.0, 1.5),
            'temperature': np.random.normal(75, 10),
            'bearing_temperature': np.random.normal(65, 8),
            'roll_wear': np.random.uniform(0.1, 0.4)
        }
        
        maintenance_prediction = self.maintenance_model.predict_maintenance(maintenance_data)
        
        if maintenance_prediction['risk_level'] in ['high', 'critical']:
            batch.maintenance_alerts.append({
                'equipment': 'rolling_mill',
                'alert': maintenance_prediction,
                'stage': ProductionStage.ROLLING
            })
        
        # Simulate rolling parameters
        rolling_params = {
            'billet_temperature': np.random.normal(1050, 50),
            'rolling_speed': np.random.normal(8.0, 1.5),
            'total_reduction': np.random.normal(85, 5),
            'final_diameter': 16,  # mm
            'rib_height': np.random.normal(0.7, 0.1),
            'rib_spacing': np.random.normal(11.2, 0.5)
        }
        
        batch.process_parameters['rolling'] = rolling_params
        
        # Predict quality using ML model
        quality_prediction_data = {
            **batch.process_parameters.get('scrap_composition', {}),
            **rolling_params,
            'grade': batch.grade,
            'surface_quality_score': np.random.normal(90, 5)
        }
        
        quality_prediction = self.quality_model.predict_quality(quality_prediction_data)
        batch.quality_metrics['rolling_quality'] = quality_prediction
        
        # Detect potential defects
        defect_detection_data = {
            'surface_roughness': np.random.normal(2.5, 0.5),
            'rib_height': rolling_params['rib_height'],
            'rib_spacing': rolling_params['rib_spacing'],
            'surface_temperature': rolling_params['billet_temperature'] * 0.05,  # Approximation
            'diameter_deviation': np.random.normal(0.1, 0.05)
        }
        
        defect_detection = self.defect_model.detect_defects(defect_detection_data)
        
        if defect_detection['primary_defect'] != 'no_defect':
            batch.defect_history.append({
                'stage': ProductionStage.ROLLING,
                'detection': defect_detection,
                'parameters': defect_detection_data
            })
        
        return {
            'status': 'completed',
            'parameters': rolling_params,
            'quality_prediction': quality_prediction,
            'defect_detection': defect_detection,
            'maintenance_risk': maintenance_prediction['risk_level']
        }
    
    def process_heat_treatment(self, batch: ProductionBatch) -> Dict:
        """Simulate heat treatment process"""
        heat_treatment_params = {
            'austenitizing_temp': np.random.normal(900, 30),
            'quench_rate': np.random.normal(50, 10),
            'tempering_temp': np.random.normal(600, 50),
            'cooling_rate': np.random.normal(25, 5),
            'atmosphere_quality': np.random.normal(92, 3)
        }
        
        batch.process_parameters['heat_treatment'] = heat_treatment_params
        
        # Update quality prediction with heat treatment parameters
        if 'rolling_quality' in batch.quality_metrics:
            updated_prediction_data = {
                **batch.process_parameters.get('scrap_composition', {}),
                **batch.process_parameters.get('rolling', {}),
                **heat_treatment_params,
                'grade': batch.grade
            }
            
            updated_quality = self.quality_model.predict_quality(updated_prediction_data)
            batch.quality_metrics['final_quality'] = updated_quality
        
        return {
            'status': 'completed',
            'parameters': heat_treatment_params,
            'final_quality': batch.quality_metrics.get('final_quality')
        }
    
    def process_quality_control(self, batch: ProductionBatch) -> Dict:
        """Simulate quality control testing"""
        # Simulate tensile testing
        if 'final_quality' in batch.quality_metrics:
            predicted_props = batch.quality_metrics['final_quality']
            
            # Add some noise to simulate actual test results
            actual_yield = predicted_props['predicted_yield_strength'] + np.random.normal(0, 20)
            actual_tensile = predicted_props['predicted_tensile_strength'] + np.random.normal(0, 30)
            actual_elongation = predicted_props['predicted_elongation'] + np.random.normal(0, 1)
            
            # Check ASTM compliance
            astm_specs = self.quality_model.astm_specs.get(batch.grade, {})
            
            compliance_checks = {
                'yield_strength_pass': actual_yield >= astm_specs.get('min_yield', 0),
                'tensile_strength_pass': actual_tensile >= astm_specs.get('min_tensile', 0),
                'elongation_pass': actual_elongation >= astm_specs.get('min_elongation', 0)
            }
            
            overall_compliance = all(compliance_checks.values())
            
            qc_results = {
                'actual_yield_strength': actual_yield,
                'actual_tensile_strength': actual_tensile,
                'actual_elongation': actual_elongation,
                'compliance_checks': compliance_checks,
                'astm_compliant': overall_compliance,
                'test_certificate': f"TC-{batch.batch_id}-{int(time.time())}"
            }
            
            batch.quality_metrics['qc_results'] = qc_results
            
            return {
                'status': 'completed' if overall_compliance else 'non_conforming',
                'results': qc_results,
                'compliance': overall_compliance
            }
        
        return {'status': 'error', 'message': 'No quality predictions available'}
    
    def advance_stage(self, batch: ProductionBatch) -> bool:
        """Advance batch to next production stage"""
        current_stage_index = list(ProductionStage).index(batch.current_stage)
        
        if current_stage_index < len(ProductionStage) - 1:
            next_stage = list(ProductionStage)[current_stage_index + 1]
            batch.current_stage = next_stage
            batch.stage_timestamps[next_stage] = time.time()
            return True
        
        return False  # Already at final stage
    
    def run_complete_workflow(self, batch_id: str, grade: str, quantity: float) -> Dict:
        """Run complete production workflow for a batch"""
        batch = self.create_production_batch(batch_id, grade, quantity)
        workflow_results = {}
        
        # Process each stage
        stage_processors = {
            ProductionStage.RAW_MATERIAL: self.process_raw_materials,
            ProductionStage.EAF_MELTING: self.process_eaf_melting,
            ProductionStage.CASTING: self.process_casting,
            ProductionStage.ROLLING: self.process_rolling,
            ProductionStage.HEAT_TREATMENT: self.process_heat_treatment,
            ProductionStage.QUALITY_CONTROL: self.process_quality_control
        }
        
        for stage in stage_processors:
            if batch.current_stage == stage:
                result = stage_processors[stage](batch)
                workflow_results[stage.value] = result
                
                if result['status'] == 'non_conforming':
                    workflow_results['final_status'] = 'rejected'
                    break
                
                if not self.advance_stage(batch):
                    break
        
        # Calculate total processing time
        start_time = batch.stage_timestamps[ProductionStage.RAW_MATERIAL]
        end_time = time.time()
        total_time = end_time - start_time
        
        workflow_results['summary'] = {
            'batch_id': batch.batch_id,
            'grade': batch.grade,
            'target_quantity': batch.target_quantity,
            'final_quantity': batch.current_quantity,
            'yield_rate': batch.current_quantity / batch.target_quantity if batch.target_quantity > 0 else 0,
            'total_processing_time': total_time,
            'stages_completed': len(workflow_results) - 1,  # Exclude summary
            'maintenance_alerts': len(batch.maintenance_alerts),
            'defects_detected': len(batch.defect_history),
            'final_compliance': batch.quality_metrics.get('qc_results', {}).get('astm_compliant', False)
        }
        
        workflow_results['batch'] = batch
        
        return workflow_results


class TestEndToEndWorkflow(unittest.TestCase):
    """Test suite for end-to-end workflow simulation"""
    
    @classmethod
    def setUpClass(cls):
        """Set up test fixtures for the entire test class"""
        cls.simulator = RebarProductionSimulator()
    
    def test_single_batch_complete_workflow(self):
        """Test complete workflow for a single production batch"""
        print(f"🏭 Testing complete production workflow...")
        
        # Run complete workflow
        results = self.simulator.run_complete_workflow(
            batch_id="TEST-001",
            grade="Grade60",
            quantity=50.0  # tons
        )
        
        # Validate workflow completion
        self.assertIn('summary', results)
        summary = results['summary']
        
        self.assertEqual(summary['batch_id'], "TEST-001")
        self.assertEqual(summary['grade'], "Grade60")
        self.assertEqual(summary['target_quantity'], 50.0)
        
        # Should complete multiple stages
        self.assertGreaterEqual(summary['stages_completed'], 5)
        
        # Should have reasonable yield
        self.assertGreater(summary['yield_rate'], 0.8)
        self.assertLessEqual(summary['yield_rate'], 1.0)
        
        # Should have processing time
        self.assertGreater(summary['total_processing_time'], 0)
        
        print(f"✅ Single Batch Workflow Results:")
        print(f"   Batch ID: {summary['batch_id']}")
        print(f"   Grade: {summary['grade']}")
        print(f"   Yield rate: {summary['yield_rate']:.1%}")
        print(f"   Stages completed: {summary['stages_completed']}")
        print(f"   Maintenance alerts: {summary['maintenance_alerts']}")
        print(f"   Defects detected: {summary['defects_detected']}")
        print(f"   ASTM compliant: {summary['final_compliance']}")
    
    def test_multiple_grade_production(self):
        """Test production workflow for different rebar grades"""
        grades = ['Grade40', 'Grade60', 'Grade75', 'Grade80']
        results_by_grade = {}
        
        for grade in grades:
            batch_id = f"GRADE-{grade[-2:]}-001"
            results = self.simulator.run_complete_workflow(
                batch_id=batch_id,
                grade=grade,
                quantity=30.0
            )
            
            results_by_grade[grade] = results['summary']
            
            # Each grade should complete successfully
            self.assertGreaterEqual(results['summary']['stages_completed'], 5)
            
            # Quality predictions should be grade-appropriate
            if 'rolling' in results:
                quality_pred = results['rolling']['quality_prediction']
                if grade == 'Grade80':
                    self.assertGreater(quality_pred['predicted_yield_strength'], 500)
                elif grade == 'Grade40':
                    self.assertLess(quality_pred['predicted_yield_strength'], 400)
        
        print(f"✅ Multiple Grade Production:")
        for grade, summary in results_by_grade.items():
            print(f"   {grade}: Yield={summary['yield_rate']:.1%}, "
                  f"Compliant={summary['final_compliance']}, "
                  f"Alerts={summary['maintenance_alerts']}")
    
    def test_quality_prediction_integration(self):
        """Test integration of ML quality predictions throughout workflow"""
        results = self.simulator.run_complete_workflow(
            batch_id="QP-001",
            grade="Grade60",
            quantity=25.0
        )
        
        batch = results['batch']
        
        # Should have quality predictions at rolling stage
        self.assertIn('rolling_quality', batch.quality_metrics)
        rolling_quality = batch.quality_metrics['rolling_quality']
        
        self.assertIn('astm_compliant', rolling_quality)
        self.assertIn('quality_score', rolling_quality)
        self.assertIn('predicted_yield_strength', rolling_quality)
        
        # Should have final quality after heat treatment
        if 'final_quality' in batch.quality_metrics:
            final_quality = batch.quality_metrics['final_quality']
            
            self.assertIn('astm_compliant', final_quality)
            self.assertIn('quality_score', final_quality)
        
        # Quality predictions should influence QC results
        if 'qc_results' in batch.quality_metrics:
            qc_results = batch.quality_metrics['qc_results']
            
            self.assertIn('astm_compliant', qc_results)
            self.assertIn('actual_yield_strength', qc_results)
            self.assertIn('test_certificate', qc_results)
        
        print(f"✅ Quality Prediction Integration:")
        print(f"   Rolling quality score: {rolling_quality.get('quality_score', 'N/A'):.1f}")
        print(f"   Predicted yield strength: {rolling_quality.get('predicted_yield_strength', 'N/A'):.0f} MPa")
        if 'qc_results' in batch.quality_metrics:
            print(f"   Actual yield strength: {batch.quality_metrics['qc_results']['actual_yield_strength']:.0f} MPa")
    
    def test_defect_detection_integration(self):
        """Test integration of ML defect detection throughout workflow"""
        # Run multiple batches to increase chance of detecting defects
        total_defects = 0
        total_batches = 5
        
        for i in range(total_batches):
            results = self.simulator.run_complete_workflow(
                batch_id=f"DD-{i+1:03d}",
                grade="Grade60",
                quantity=20.0
            )
            
            batch = results['batch']
            batch_defects = len(batch.defect_history)
            total_defects += batch_defects
            
            # If defects detected, validate structure
            if batch_defects > 0:
                for defect in batch.defect_history:
                    self.assertIn('stage', defect)
                    self.assertIn('detection', defect)
                    self.assertIn('parameters', defect)
                    
                    detection = defect['detection']
                    self.assertIn('primary_defect', detection)
                    self.assertIn('severity', detection)
                    self.assertIn('confidence', detection)
        
        print(f"✅ Defect Detection Integration:")
        print(f"   Total batches processed: {total_batches}")
        print(f"   Total defects detected: {total_defects}")
        print(f"   Average defects per batch: {total_defects / total_batches:.2f}")
    
    def test_maintenance_prediction_integration(self):
        """Test integration of ML maintenance predictions"""
        # Run workflow and check for maintenance alerts
        results = self.simulator.run_complete_workflow(
            batch_id="MP-001",
            grade="Grade75",
            quantity=40.0
        )
        
        batch = results['batch']
        maintenance_alerts = batch.maintenance_alerts
        
        # Validate maintenance alert structure if any exist
        for alert in maintenance_alerts:
            self.assertIn('equipment', alert)
            self.assertIn('alert', alert)
            self.assertIn('stage', alert)
            
            alert_data = alert['alert']
            self.assertIn('risk_level', alert_data)
            self.assertIn('failure_probability', alert_data)
            self.assertIn('estimated_rul_days', alert_data)
        
        print(f"✅ Maintenance Prediction Integration:")
        print(f"   Maintenance alerts generated: {len(maintenance_alerts)}")
        for alert in maintenance_alerts:
            print(f"   - {alert['equipment']}: {alert['alert']['risk_level']} risk at {alert['stage'].value}")
    
    def test_batch_processing_workflow(self):
        """Test processing of multiple batches concurrently"""
        batch_configs = [
            ("BATCH-001", "Grade40", 25.0),
            ("BATCH-002", "Grade60", 30.0),
            ("BATCH-003", "Grade60", 35.0),
            ("BATCH-004", "Grade75", 20.0),
            ("BATCH-005", "Grade80", 15.0)
        ]
        
        batch_results = []
        
        for batch_id, grade, quantity in batch_configs:
            results = self.simulator.run_complete_workflow(batch_id, grade, quantity)
            batch_results.append(results['summary'])
        
        # Analyze batch processing results
        total_quantity = sum(config[2] for config in batch_configs)
        actual_quantity = sum(result['final_quantity'] for result in batch_results)
        overall_yield = actual_quantity / total_quantity
        
        compliant_batches = sum(1 for result in batch_results if result['final_compliance'])
        compliance_rate = compliant_batches / len(batch_results)
        
        average_alerts = np.mean([result['maintenance_alerts'] for result in batch_results])
        average_defects = np.mean([result['defects_detected'] for result in batch_results])
        
        # Validate batch processing performance
        self.assertGreater(overall_yield, 0.85, "Overall yield too low")
        self.assertGreater(compliance_rate, 0.7, "Compliance rate too low")
        
        print(f"✅ Batch Processing Results:")
        print(f"   Batches processed: {len(batch_configs)}")
        print(f"   Total target quantity: {total_quantity:.1f} tons")
        print(f"   Total actual quantity: {actual_quantity:.1f} tons")
        print(f"   Overall yield: {overall_yield:.1%}")
        print(f"   Compliance rate: {compliance_rate:.1%}")
        print(f"   Average maintenance alerts: {average_alerts:.1f}")
        print(f"   Average defects per batch: {average_defects:.1f}")
    
    def test_production_optimization_scenarios(self):
        """Test various production optimization scenarios"""
        scenarios = [
            {
                'name': 'High Quality Target',
                'grade': 'Grade80',
                'quantity': 20.0,
                'expected_yield': 0.92
            },
            {
                'name': 'High Volume Production',
                'grade': 'Grade60',
                'quantity': 100.0,
                'expected_yield': 0.95
            },
            {
                'name': 'Standard Production',
                'grade': 'Grade60',
                'quantity': 50.0,
                'expected_yield': 0.94
            }
        ]
        
        scenario_results = {}
        
        for scenario in scenarios:
            results = self.simulator.run_complete_workflow(
                batch_id=f"OPT-{scenario['name'].replace(' ', '-')}",
                grade=scenario['grade'],
                quantity=scenario['quantity']
            )
            
            summary = results['summary']
            scenario_results[scenario['name']] = {
                'yield': summary['yield_rate'],
                'compliance': summary['final_compliance'],
                'processing_time': summary['total_processing_time'],
                'quality_score': results.get('rolling', {}).get('quality_prediction', {}).get('quality_score', 0)
            }
            
            # Basic validation
            self.assertGreater(summary['yield_rate'], 0.8)
            self.assertGreater(summary['stages_completed'], 4)
        
        print(f"✅ Production Optimization Scenarios:")
        for scenario_name, metrics in scenario_results.items():
            print(f"   {scenario_name}:")
            print(f"     Yield: {metrics['yield']:.1%}")
            print(f"     Compliance: {metrics['compliance']}")
            print(f"     Quality Score: {metrics['quality_score']:.1f}")
    
    def test_workflow_error_handling(self):
        """Test workflow error handling and recovery"""
        # Test with extreme parameters that might cause issues
        edge_cases = [
            ("EDGE-001", "Grade60", 1.0),    # Very small batch
            ("EDGE-002", "Grade80", 200.0),  # Very large batch
            ("EDGE-003", "Grade40", 0.1),    # Tiny batch
        ]
        
        successful_runs = 0
        
        for batch_id, grade, quantity in edge_cases:
            try:
                results = self.simulator.run_complete_workflow(batch_id, grade, quantity)
                summary = results['summary']
                
                # Basic validation for edge cases
                self.assertIsInstance(summary['yield_rate'], float)
                self.assertGreaterEqual(summary['yield_rate'], 0)
                self.assertLessEqual(summary['yield_rate'], 1.2)  # Allow some margin
                
                successful_runs += 1
                
            except Exception as e:
                print(f"⚠️  Edge case {batch_id} failed: {str(e)}")
        
        # At least most edge cases should handle gracefully
        self.assertGreaterEqual(successful_runs, len(edge_cases) * 0.8)
        
        print(f"✅ Error Handling:")
        print(f"   Edge cases tested: {len(edge_cases)}")
        print(f"   Successful runs: {successful_runs}")
        print(f"   Success rate: {successful_runs / len(edge_cases):.1%}")
    
    def test_workflow_performance_metrics(self):
        """Test workflow performance and timing metrics"""
        # Measure workflow performance
        batch_sizes = [10.0, 50.0, 100.0]
        performance_metrics = {}
        
        for batch_size in batch_sizes:
            start_time = time.time()
            
            results = self.simulator.run_complete_workflow(
                batch_id=f"PERF-{int(batch_size)}",
                grade="Grade60",
                quantity=batch_size
            )
            
            execution_time = time.time() - start_time
            summary = results['summary']
            
            performance_metrics[batch_size] = {
                'execution_time': execution_time,
                'processing_time': summary['total_processing_time'],
                'stages_completed': summary['stages_completed'],
                'yield': summary['yield_rate']
            }
            
            # Performance should be reasonable
            self.assertLess(execution_time, 10.0, f"Workflow too slow for {batch_size}t batch")
            self.assertGreater(summary['stages_completed'], 4)
        
        print(f"✅ Workflow Performance Metrics:")
        for batch_size, metrics in performance_metrics.items():
            print(f"   {batch_size}t batch:")
            print(f"     Execution time: {metrics['execution_time']:.3f}s")
            print(f"     Stages completed: {metrics['stages_completed']}")
            print(f"     Yield: {metrics['yield']:.1%}")


if __name__ == '__main__':
    # Run tests with detailed output
    unittest.main(verbosity=2)