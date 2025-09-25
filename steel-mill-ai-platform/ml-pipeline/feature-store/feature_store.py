#!/usr/bin/env python3
"""
Feature Store for Steel Mill AI Platform
Centralized feature engineering, storage, and serving
"""
import asyncio
import json
import time
from datetime import datetime, timedelta
from typing import Dict, List, Any, Optional, Union
from dataclasses import dataclass, asdict
from abc import ABC, abstractmethod

import pandas as pd
import numpy as np
import redis.asyncio as redis
from sqlalchemy import create_engine, text
from sqlalchemy.pool import QueuePool
import influxdb_client
from influxdb_client.client.query_api import QueryApi
from influxdb_client.client.write_api import SYNCHRONOUS

@dataclass
class FeatureMetadata:
    name: str
    data_type: str
    description: str
    source_table: str
    transformation: str
    freshness_sla_minutes: int
    created_at: datetime
    updated_at: datetime
    version: str = "1.0"
    tags: List[str] = None

@dataclass
class FeatureValue:
    feature_name: str
    entity_id: str
    value: Union[float, int, str, bool]
    timestamp: datetime
    source: str

class FeatureTransformer:
    """Feature transformation functions"""
    
    @staticmethod
    def rolling_average(values: List[float], window: int = 5) -> float:
        """Calculate rolling average"""
        if len(values) < window:
            return np.mean(values) if values else 0.0
        return np.mean(values[-window:])
    
    @staticmethod
    def rolling_std(values: List[float], window: int = 5) -> float:
        """Calculate rolling standard deviation"""
        if len(values) < window:
            return np.std(values) if len(values) > 1 else 0.0
        return np.std(values[-window:])
    
    @staticmethod
    def z_score_normalization(value: float, mean: float, std: float) -> float:
        """Z-score normalization"""
        return (value - mean) / (std + 1e-8)
    
    @staticmethod
    def min_max_normalization(value: float, min_val: float, max_val: float) -> float:
        """Min-max normalization"""
        return (value - min_val) / (max_val - min_val + 1e-8)
    
    @staticmethod
    def rate_of_change(current: float, previous: float, time_diff_seconds: float) -> float:
        """Calculate rate of change per second"""
        if time_diff_seconds <= 0:
            return 0.0
        return (current - previous) / time_diff_seconds
    
    @staticmethod
    def temperature_efficiency_ratio(temp: float, power: float) -> float:
        """Steel mill specific: temperature to power efficiency ratio"""
        if power <= 0:
            return 0.0
        return temp / power
    
    @staticmethod
    def vibration_magnitude(x: float, y: float, z: float) -> float:
        """Calculate 3D vibration magnitude"""
        return np.sqrt(x**2 + y**2 + z**2)
    
    @staticmethod
    def energy_consumption_score(power: float, production_rate: float) -> float:
        """Energy efficiency score"""
        if production_rate <= 0:
            return 0.0
        return power / production_rate

class FeatureStore:
    """Centralized feature store for Steel Mill AI Platform"""
    
    def __init__(self, 
                 redis_host: str = "localhost",
                 redis_port: int = 6379,
                 influxdb_url: str = "http://localhost:8086",
                 influxdb_token: str = "",
                 influxdb_org: str = "steel-mill",
                 influxdb_bucket: str = "features"):
        
        self.redis_client = None
        self.influx_client = None
        self.influx_write_api = None
        self.influx_query_api = None
        self.transformer = FeatureTransformer()
        
        # Configuration
        self.redis_host = redis_host
        self.redis_port = redis_port
        self.influxdb_url = influxdb_url
        self.influxdb_token = influxdb_token
        self.influxdb_org = influxdb_org
        self.influxdb_bucket = influxdb_bucket
        
        # Feature registry
        self.feature_registry: Dict[str, FeatureMetadata] = {}
        
        # Default features for steel mill
        self._register_default_features()
    
    async def initialize(self):
        """Initialize connections to storage backends"""
        # Initialize Redis for real-time feature serving
        try:
            self.redis_client = redis.Redis(
                host=self.redis_host,
                port=self.redis_port,
                decode_responses=True,
                socket_connect_timeout=5
            )
            await self.redis_client.ping()
            print("✅ Connected to Redis feature cache")
        except Exception as e:
            print(f"❌ Redis connection failed: {e}")
        
        # Initialize InfluxDB for time-series features
        try:
            self.influx_client = influxdb_client.InfluxDBClient(
                url=self.influxdb_url,
                token=self.influxdb_token,
                org=self.influxdb_org
            )
            self.influx_write_api = self.influx_client.write_api(write_options=SYNCHRONOUS)
            self.influx_query_api = self.influx_client.query_api()
            
            # Test connection
            health = self.influx_client.health()
            if health.status == "pass":
                print("✅ Connected to InfluxDB feature store")
            else:
                print(f"⚠️ InfluxDB health check failed: {health}")
        except Exception as e:
            print(f"❌ InfluxDB connection failed: {e}")
    
    def _register_default_features(self):
        """Register default features for steel mill operations"""
        default_features = [
            # Temperature features
            FeatureMetadata(
                name="temperature_rolling_avg_5min",
                data_type="float",
                description="5-minute rolling average temperature",
                source_table="sensor_telemetry",
                transformation="rolling_average(temperature, 5)",
                freshness_sla_minutes=1,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["temperature", "rolling", "furnace"]
            ),
            FeatureMetadata(
                name="temperature_rate_of_change",
                data_type="float",
                description="Rate of temperature change per second",
                source_table="sensor_telemetry",
                transformation="rate_of_change(temperature)",
                freshness_sla_minutes=1,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["temperature", "derivative", "furnace"]
            ),
            
            # Vibration features
            FeatureMetadata(
                name="vibration_3d_magnitude",
                data_type="float",
                description="3D vibration magnitude",
                source_table="sensor_telemetry",
                transformation="sqrt(vibX^2 + vibY^2 + vibZ^2)",
                freshness_sla_minutes=1,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["vibration", "magnitude", "rolling_mill"]
            ),
            FeatureMetadata(
                name="vibration_z_score",
                data_type="float",
                description="Z-score normalized vibration",
                source_table="sensor_telemetry",
                transformation="z_score_normalization(vibration)",
                freshness_sla_minutes=1,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["vibration", "normalized", "rolling_mill"]
            ),
            
            # Energy features
            FeatureMetadata(
                name="energy_efficiency_score",
                data_type="float",
                description="Energy consumption per unit production",
                source_table="sensor_telemetry",
                transformation="power_draw / production_rate",
                freshness_sla_minutes=5,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["energy", "efficiency", "production"]
            ),
            FeatureMetadata(
                name="power_rolling_std_10min",
                data_type="float",
                description="10-minute rolling standard deviation of power",
                source_table="sensor_telemetry", 
                transformation="rolling_std(power_draw, 10)",
                freshness_sla_minutes=2,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["power", "variability", "energy"]
            ),
            
            # Time-based features
            FeatureMetadata(
                name="hour_of_day_sin",
                data_type="float",
                description="Sine transformation of hour of day",
                source_table="computed",
                transformation="sin(2*pi*hour/24)",
                freshness_sla_minutes=60,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["time", "cyclical", "temporal"]
            ),
            FeatureMetadata(
                name="hour_of_day_cos",
                data_type="float",
                description="Cosine transformation of hour of day", 
                source_table="computed",
                transformation="cos(2*pi*hour/24)",
                freshness_sla_minutes=60,
                created_at=datetime.now(),
                updated_at=datetime.now(),
                tags=["time", "cyclical", "temporal"]
            )
        ]
        
        for feature in default_features:
            self.feature_registry[feature.name] = feature
    
    async def register_feature(self, feature: FeatureMetadata):
        """Register a new feature in the feature store"""
        self.feature_registry[feature.name] = feature
        
        # Store in Redis for quick lookup
        if self.redis_client:
            await self.redis_client.hset(
                "feature_registry",
                feature.name,
                json.dumps(asdict(feature), default=str)
            )
    
    async def compute_features(self, 
                             entity_id: str, 
                             raw_data: Dict[str, Any],
                             timestamp: datetime = None) -> Dict[str, FeatureValue]:
        """Compute features from raw sensor data"""
        if timestamp is None:
            timestamp = datetime.now()
        
        computed_features = {}
        
        # Get historical data for rolling calculations
        historical_data = await self._get_historical_data(entity_id, lookback_minutes=15)
        
        # Compute each registered feature
        for feature_name, feature_meta in self.feature_registry.items():
            try:
                value = await self._compute_single_feature(
                    feature_meta, entity_id, raw_data, historical_data, timestamp
                )
                
                computed_features[feature_name] = FeatureValue(
                    feature_name=feature_name,
                    entity_id=entity_id,
                    value=value,
                    timestamp=timestamp,
                    source="computed"
                )
            except Exception as e:
                print(f"Error computing feature {feature_name}: {e}")
                # Set default value on error
                computed_features[feature_name] = FeatureValue(
                    feature_name=feature_name,
                    entity_id=entity_id,
                    value=0.0,
                    timestamp=timestamp,
                    source="default"
                )
        
        return computed_features
    
    async def _compute_single_feature(self, 
                                    feature_meta: FeatureMetadata,
                                    entity_id: str,
                                    raw_data: Dict[str, Any],
                                    historical_data: pd.DataFrame,
                                    timestamp: datetime) -> float:
        """Compute a single feature based on its transformation"""
        
        # Temperature features
        if feature_meta.name == "temperature_rolling_avg_5min":
            temp_values = historical_data['temperature'].dropna().tolist()
            temp_values.append(raw_data.get('temperature', 0))
            return self.transformer.rolling_average(temp_values, window=5)
        
        elif feature_meta.name == "temperature_rate_of_change":
            if len(historical_data) > 0:
                last_temp = historical_data['temperature'].iloc[-1]
                last_time = historical_data['timestamp'].iloc[-1]
                current_temp = raw_data.get('temperature', last_temp)
                time_diff = (timestamp - pd.to_datetime(last_time)).total_seconds()
                return self.transformer.rate_of_change(current_temp, last_temp, time_diff)
            return 0.0
        
        # Vibration features
        elif feature_meta.name == "vibration_3d_magnitude":
            vib_x = raw_data.get('vibrationX', 0)
            vib_y = raw_data.get('vibrationY', 0)
            vib_z = raw_data.get('vibrationZ', 0)
            return self.transformer.vibration_magnitude(vib_x, vib_y, vib_z)
        
        elif feature_meta.name == "vibration_z_score":
            vibration = raw_data.get('vibration', 0)
            if len(historical_data) > 1:
                hist_vibration = historical_data['vibration'].dropna()
                mean_vib = hist_vibration.mean()
                std_vib = hist_vibration.std()
                return self.transformer.z_score_normalization(vibration, mean_vib, std_vib)
            return 0.0
        
        # Energy features
        elif feature_meta.name == "energy_efficiency_score":
            power = raw_data.get('powerDraw', 0)
            production_rate = raw_data.get('speed', 1)  # Use speed as production proxy
            return self.transformer.energy_consumption_score(power, production_rate)
        
        elif feature_meta.name == "power_rolling_std_10min":
            power_values = historical_data['powerDraw'].dropna().tolist()
            power_values.append(raw_data.get('powerDraw', 0))
            return self.transformer.rolling_std(power_values, window=10)
        
        # Time features
        elif feature_meta.name == "hour_of_day_sin":
            hour = timestamp.hour
            return np.sin(2 * np.pi * hour / 24)
        
        elif feature_meta.name == "hour_of_day_cos":
            hour = timestamp.hour
            return np.cos(2 * np.pi * hour / 24)
        
        # Default fallback
        return 0.0
    
    async def _get_historical_data(self, entity_id: str, lookback_minutes: int = 15) -> pd.DataFrame:
        """Get historical data for feature computation"""
        if not self.influx_query_api:
            return pd.DataFrame()
        
        try:
            query = f'''
            from(bucket: "{self.influxdb_bucket}")
              |> range(start: -{lookback_minutes}m)
              |> filter(fn: (r) => r["entity_id"] == "{entity_id}")
              |> pivot(rowKey:["_time"], columnKey: ["_field"], valueColumn: "_value")
            '''
            
            result = self.influx_query_api.query_data_frame(query)
            if not result.empty:
                result['timestamp'] = pd.to_datetime(result['_time'])
                return result
            
        except Exception as e:
            print(f"Error querying historical data: {e}")
        
        return pd.DataFrame()
    
    async def store_features(self, features: Dict[str, FeatureValue]):
        """Store computed features in both cache and time-series database"""
        # Store in Redis cache for real-time serving
        if self.redis_client:
            pipe = self.redis_client.pipeline()
            for feature_name, feature_value in features.items():
                cache_key = f"feature:{feature_value.entity_id}:{feature_name}"
                feature_dict = asdict(feature_value)
                feature_dict['timestamp'] = feature_dict['timestamp'].isoformat()
                
                pipe.setex(
                    cache_key,
                    300,  # 5 minutes TTL
                    json.dumps(feature_dict)
                )
            
            await pipe.execute()
        
        # Store in InfluxDB for historical analysis
        if self.influx_write_api:
            points = []
            for feature_name, feature_value in features.items():
                point = influxdb_client.Point("features") \
                    .tag("entity_id", feature_value.entity_id) \
                    .tag("feature_name", feature_name) \
                    .field("value", float(feature_value.value)) \
                    .time(feature_value.timestamp)
                points.append(point)
            
            try:
                self.influx_write_api.write(bucket=self.influxdb_bucket, record=points)
            except Exception as e:
                print(f"Error writing to InfluxDB: {e}")
    
    async def get_features(self, 
                          entity_id: str, 
                          feature_names: List[str],
                          timestamp: datetime = None) -> Dict[str, Any]:
        """Get features for an entity (from cache first, then compute if needed)"""
        if timestamp is None:
            timestamp = datetime.now()
        
        features = {}
        missing_features = []
        
        # Try to get from Redis cache first
        if self.redis_client:
            for feature_name in feature_names:
                cache_key = f"feature:{entity_id}:{feature_name}"
                cached_value = await self.redis_client.get(cache_key)
                
                if cached_value:
                    feature_data = json.loads(cached_value)
                    cached_time = datetime.fromisoformat(feature_data['timestamp'])
                    
                    # Check if cached value is fresh enough
                    feature_meta = self.feature_registry.get(feature_name)
                    if feature_meta:
                        max_age = timedelta(minutes=feature_meta.freshness_sla_minutes)
                        if timestamp - cached_time <= max_age:
                            features[feature_name] = feature_data['value']
                            continue
                
                missing_features.append(feature_name)
        else:
            missing_features = feature_names
        
        # For missing features, compute from raw data (would need raw data as parameter)
        # This is a simplified version - in practice, you'd fetch latest raw data
        for feature_name in missing_features:
            features[feature_name] = 0.0  # Placeholder
        
        return features
    
    async def get_feature_vector(self, 
                                entity_id: str, 
                                feature_names: List[str]) -> List[float]:
        """Get feature vector for ML model input"""
        features = await self.get_features(entity_id, feature_names)
        return [features.get(name, 0.0) for name in feature_names]
    
    async def get_batch_features(self, 
                               entity_ids: List[str], 
                               feature_names: List[str]) -> Dict[str, Dict[str, Any]]:
        """Get features for multiple entities efficiently"""
        batch_results = {}
        
        # Process in batches to avoid overwhelming the system
        batch_size = 50
        for i in range(0, len(entity_ids), batch_size):
            batch = entity_ids[i:i + batch_size]
            tasks = [self.get_features(entity_id, feature_names) for entity_id in batch]
            batch_features = await asyncio.gather(*tasks)
            
            for entity_id, features in zip(batch, batch_features):
                batch_results[entity_id] = features
        
        return batch_results
    
    def list_features(self, tags: List[str] = None) -> List[FeatureMetadata]:
        """List available features, optionally filtered by tags"""
        features = list(self.feature_registry.values())
        
        if tags:
            filtered_features = []
            for feature in features:
                if feature.tags and any(tag in feature.tags for tag in tags):
                    filtered_features.append(feature)
            return filtered_features
        
        return features
    
    async def get_feature_statistics(self, 
                                   feature_name: str, 
                                   entity_id: str = None,
                                   days: int = 7) -> Dict[str, Any]:
        """Get statistics for a feature over a time period"""
        if not self.influx_query_api:
            return {}
        
        entity_filter = f'|> filter(fn: (r) => r["entity_id"] == "{entity_id}")' if entity_id else ''
        
        query = f'''
        from(bucket: "{self.influxdb_bucket}")
          |> range(start: -{days}d)
          |> filter(fn: (r) => r["feature_name"] == "{feature_name}")
          {entity_filter}
          |> group(columns: ["feature_name"])
          |> aggregateWindow(every: 1h, fn: mean, createEmpty: false)
        '''
        
        try:
            result = self.influx_query_api.query_data_frame(query)
            if not result.empty:
                values = result['_value'].dropna()
                return {
                    "mean": float(values.mean()),
                    "std": float(values.std()),
                    "min": float(values.min()),
                    "max": float(values.max()),
                    "count": int(len(values)),
                    "percentiles": {
                        "p25": float(values.quantile(0.25)),
                        "p50": float(values.quantile(0.50)),
                        "p75": float(values.quantile(0.75)),
                        "p95": float(values.quantile(0.95))
                    }
                }
        except Exception as e:
            print(f"Error getting feature statistics: {e}")
        
        return {}
    
    async def cleanup_old_features(self, retention_days: int = 30):
        """Clean up old feature data"""
        if not self.influx_client:
            return
        
        try:
            start_time = datetime.now() - timedelta(days=retention_days)
            stop_time = datetime.now()
            
            delete_api = self.influx_client.delete_api()
            delete_api.delete(
                start_time,
                stop_time,
                predicate='_measurement="features"',
                bucket=self.influxdb_bucket,
                org=self.influxdb_org
            )
            
            print(f"Cleaned up features older than {retention_days} days")
            
        except Exception as e:
            print(f"Error during cleanup: {e}")

# Usage example and testing
async def main():
    feature_store = FeatureStore()
    await feature_store.initialize()
    
    # Example: Process new sensor data
    raw_sensor_data = {
        'temperature': 1520.5,
        'vibrationX': 0.05,
        'vibrationY': 0.06,
        'vibrationZ': 0.04,
        'powerDraw': 2650.0,
        'speed': 12.5
    }
    
    # Compute features
    features = await feature_store.compute_features("furnace-001", raw_sensor_data)
    print("Computed features:", {k: v.value for k, v in features.items()})
    
    # Store features
    await feature_store.store_features(features)
    
    # Retrieve features
    feature_names = ["temperature_rolling_avg_5min", "vibration_3d_magnitude", "energy_efficiency_score"]
    retrieved = await feature_store.get_features("furnace-001", feature_names)
    print("Retrieved features:", retrieved)

if __name__ == "__main__":
    asyncio.run(main())