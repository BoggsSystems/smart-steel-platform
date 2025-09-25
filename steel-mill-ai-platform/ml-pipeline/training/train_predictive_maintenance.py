#!/usr/bin/env python3
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pandas as pd
import numpy as np
from models.predictive_maintenance import PredictiveMaintenanceModel
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, confusion_matrix
import joblib
import argparse

def load_and_prepare_data(csv_path: str):
    """Load and prepare data for predictive maintenance model"""
    print(f"Loading data from {csv_path}")
    df = pd.read_csv(csv_path)
    
    rolling_mill_data = df[df['sensorType'] == 'rolling_mill'].copy()
    conveyor_data = df[df['sensorType'] == 'conveyor'].copy()
    
    all_data = pd.concat([rolling_mill_data, conveyor_data], ignore_index=True)
    
    if len(all_data) == 0:
        print("No rolling mill or conveyor data found. Using all data.")
        all_data = df
    
    return all_data

def main():
    parser = argparse.ArgumentParser(description='Train Predictive Maintenance Model')
    parser.add_argument('--data-path', type=str, default='../../test-runner/sample_signals.csv',
                       help='Path to training data CSV')
    parser.add_argument('--model-output', type=str, default='../models',
                       help='Directory to save trained model')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.data_path):
        print(f"Data file not found: {args.data_path}")
        print("Generating synthetic data for demonstration...")
        
        synthetic_data = []
        for i in range(1000):
            synthetic_data.append({
                'deviceId': f'rolling-mill-{i % 4 + 1}',
                'vibration': np.random.uniform(0.01, 0.2),
                'vibrationX': np.random.uniform(0.05, 0.15),
                'vibrationY': np.random.uniform(0.05, 0.15),
                'vibrationZ': np.random.uniform(0.05, 0.2),
                'motorTorque': np.random.uniform(4000, 6000),
                'motorCurrent': np.random.uniform(80, 120),
                'temperature': np.random.uniform(70, 90),
                'sensorType': 'rolling_mill'
            })
        
        df = pd.DataFrame(synthetic_data)
    else:
        df = load_and_prepare_data(args.data_path)
    
    print(f"Loaded {len(df)} samples")
    
    model = PredictiveMaintenanceModel()
    
    X = model.preprocess_features(df)
    print(f"Feature matrix shape: {X.shape}")
    
    y = model.generate_synthetic_labels(X)
    print(f"Generated {sum(y)} failure risk labels out of {len(y)} total")
    
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    
    print("\nTraining model...")
    train_results = model.train(X_train, y_train)
    print(f"Training accuracy: {train_results['accuracy']:.3f}")
    print("\nFeature importance:")
    for feature, importance in sorted(train_results['feature_importance'].items(), 
                                    key=lambda x: x[1], reverse=True):
        print(f"  {feature}: {importance:.3f}")
    
    print("\nEvaluating on test set...")
    predictions, probabilities = model.predict(X_test)
    
    print("\nClassification Report:")
    print(classification_report(y_test, predictions, 
                              target_names=['Healthy', 'Failure Risk']))
    
    print("\nConfusion Matrix:")
    print(confusion_matrix(y_test, predictions))
    
    os.makedirs(args.model_output, exist_ok=True)
    model.save_model(args.model_output)
    print(f"\nModel saved to {args.model_output}/predictive_maintenance_model.pkl")
    
    print("\nExample predictions:")
    for i in range(min(5, len(X_test))):
        sample_data = {feature: X_test[i, j] 
                      for j, feature in enumerate(model.feature_names)}
        result = model.predict_single(sample_data)
        print(f"  Sample {i+1}: {result['prediction']} (confidence: {result['confidence']:.3f})")

if __name__ == '__main__':
    main()