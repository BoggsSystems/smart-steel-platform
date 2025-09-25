#!/usr/bin/env python3
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pandas as pd
import numpy as np
from models.anomaly_detection import AnomalyDetectionModel
from sklearn.metrics import classification_report
import joblib
import argparse

def load_and_prepare_data(csv_path: str):
    """Load and prepare data for anomaly detection model"""
    print(f"Loading data from {csv_path}")
    df = pd.read_csv(csv_path)
    
    furnace_data = df[df['sensorType'] == 'furnace'].copy()
    
    if len(furnace_data) == 0:
        print("No furnace data found. Using all data.")
        furnace_data = df
    
    return furnace_data

def main():
    parser = argparse.ArgumentParser(description='Train Anomaly Detection Model')
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
                'deviceId': f'furnace-{i % 3 + 1}',
                'temperature': np.random.uniform(1400, 1600),
                'vibration': np.random.uniform(0.01, 0.1),
                'oxygenMix': np.random.uniform(20, 23),
                'fuelFlow': np.random.uniform(140, 160),
                'powerDraw': np.random.uniform(2300, 2700),
                'speed': np.random.uniform(9, 11),
                'pressure': np.random.uniform(2.3, 2.7),
                'sensorType': 'furnace'
            })
        
        df = pd.DataFrame(synthetic_data)
    else:
        df = load_and_prepare_data(args.data_path)
    
    print(f"Loaded {len(df)} samples")
    
    model = AnomalyDetectionModel()
    
    X = model.preprocess_features(df)
    print(f"Feature matrix shape: {X.shape}")
    
    X_with_anomalies = model.inject_synthetic_anomalies(X, anomaly_rate=0.05)
    
    print("\nTraining model...")
    train_results = model.train(X_with_anomalies)
    print(f"Contamination rate: {train_results['contamination_rate']}")
    print(f"Average anomaly score: {train_results['avg_anomaly_score']:.3f}")
    print(f"Score threshold: {train_results['score_threshold']:.3f}")
    
    print("\nDetecting anomalies in training data...")
    predictions, scores = model.detect_anomalies(X_with_anomalies)
    
    n_anomalies = sum(predictions == -1)
    print(f"Detected {n_anomalies} anomalies ({n_anomalies/len(predictions)*100:.1f}%)")
    
    print("\nAnomaly score statistics:")
    print(f"  Min score: {scores.min():.3f}")
    print(f"  Max score: {scores.max():.3f}")
    print(f"  Mean score: {scores.mean():.3f}")
    print(f"  Std score: {scores.std():.3f}")
    
    os.makedirs(args.model_output, exist_ok=True)
    model.save_model(args.model_output)
    print(f"\nModel saved to {args.model_output}/anomaly_detection_model.pkl")
    
    print("\nExample anomaly detections:")
    anomaly_indices = np.where(predictions == -1)[0][:5]
    for idx in anomaly_indices:
        sample_data = {}
        for j, feature in enumerate(model.feature_names):
            if j < X.shape[1]:
                sample_data[feature] = X_with_anomalies[idx, j]
        
        result = model.detect_single(sample_data)
        print(f"\n  Anomaly at index {idx}:")
        print(f"    Score: {result['anomaly_score']:.3f}")
        print(f"    Top contributing features:")
        sorted_contributions = sorted(result['feature_contributions'].items(), 
                                    key=lambda x: x[1], reverse=True)[:3]
        for feature, z_score in sorted_contributions:
            print(f"      {feature}: z-score = {z_score:.2f}")

if __name__ == '__main__':
    main()