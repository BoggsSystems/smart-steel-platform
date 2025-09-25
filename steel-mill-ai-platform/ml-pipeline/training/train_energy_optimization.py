#!/usr/bin/env python3
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pandas as pd
import numpy as np
from models.energy_optimization import EnergyOptimizationModel
from sklearn.model_selection import train_test_split
from sklearn.metrics import mean_squared_error, r2_score, mean_absolute_error
import joblib
import argparse
from datetime import datetime, timedelta

def load_and_prepare_data(csv_path: str):
    """Load and prepare data for energy optimization model"""
    print(f"Loading data from {csv_path}")
    df = pd.read_csv(csv_path)
    
    if 'timestamp' not in df.columns:
        print("No timestamp column found. Generating synthetic timestamps.")
        base_time = datetime.utcnow()
        df['timestamp'] = [
            (base_time + timedelta(seconds=i*5)).isoformat() 
            for i in range(len(df))
        ]
    
    return df

def main():
    parser = argparse.ArgumentParser(description='Train Energy Optimization Model')
    parser.add_argument('--data-path', type=str, default='../../test-runner/sample_signals.csv',
                       help='Path to training data CSV')
    parser.add_argument('--model-output', type=str, default='../models',
                       help='Directory to save trained model')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.data_path):
        print(f"Data file not found: {args.data_path}")
        print("Generating synthetic data for demonstration...")
        
        synthetic_data = []
        base_time = datetime.utcnow()
        
        for i in range(1000):
            timestamp = base_time + timedelta(minutes=i*5)
            hour = timestamp.hour
            
            base_power = 2500
            if hour in [11, 12, 13, 18, 19, 20]:
                base_power += 500
            
            synthetic_data.append({
                'timestamp': timestamp.isoformat(),
                'powerDraw': base_power + np.random.uniform(-200, 200),
                'temperature': np.random.uniform(1400, 1600),
                'hour_of_day': hour,
                'sensorType': 'furnace'
            })
        
        df = pd.DataFrame(synthetic_data)
    else:
        df = load_and_prepare_data(args.data_path)
    
    print(f"Loaded {len(df)} samples")
    
    model = EnergyOptimizationModel()
    
    X = model.preprocess_features(df)
    print(f"Feature matrix shape: {X.shape}")
    
    y = model.generate_synthetic_targets(X)
    print(f"Energy consumption range: {y.min():.0f} - {y.max():.0f} kWh")
    
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    
    print("\nTraining model...")
    train_results = model.train(X_train, y_train)
    print(f"R² score: {train_results['r2_score']:.3f}")
    print(f"\nEnergy baselines:")
    for key, value in train_results['energy_baselines'].items():
        print(f"  {key}: {value:.0f} kWh")
    
    print("\nFeature importance:")
    sorted_importance = sorted(train_results['feature_importance'].items(), 
                             key=lambda x: x[1], reverse=True)
    for feature, importance in sorted_importance[:8]:
        print(f"  {feature}: {importance:.3f}")
    
    print("\nEvaluating on test set...")
    predictions = model.predict(X_test)
    
    mse = mean_squared_error(y_test, predictions)
    rmse = np.sqrt(mse)
    mae = mean_absolute_error(y_test, predictions)
    r2 = r2_score(y_test, predictions)
    
    print(f"\nTest set performance:")
    print(f"  RMSE: {rmse:.2f} kWh")
    print(f"  MAE: {mae:.2f} kWh")
    print(f"  R² score: {r2:.3f}")
    
    os.makedirs(args.model_output, exist_ok=True)
    model.save_model(args.model_output)
    print(f"\nModel saved to {args.model_output}/energy_optimization_model.pkl")
    
    print("\nExample predictions with recommendations:")
    for i in range(min(3, len(X_test))):
        sample_data = {
            'powerDraw': X_test[i, 0],
            'temperature': X_test[i, 1],
            'hour_of_day': int(X_test[i, 2]),
            'furnace_load': X_test[i, 6],
            'timestamp': datetime.utcnow().isoformat()
        }
        
        result = model.predict_single(sample_data)
        print(f"\n  Sample {i+1}:")
        print(f"    Predicted peak: {result['predicted_energy_peak']:.0f} kWh")
        print(f"    Current consumption: {result['current_consumption']:.0f} kWh")
        print(f"    Potential savings: {result['potential_savings']:.0f} kWh")
        if result['recommended_actions']:
            print(f"    Recommendations:")
            for rec in result['recommended_actions']:
                print(f"      - {rec}")

if __name__ == '__main__':
    main()