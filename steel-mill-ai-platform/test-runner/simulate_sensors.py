#!/usr/bin/env python3
import asyncio
import argparse
import json
import random
import csv
from datetime import datetime, timedelta
from typing import Dict, List, Any
import numpy as np
from azure.eventhub.aio import EventHubProducerClient
from azure.eventhub import EventData

class SensorSimulator:
    def __init__(self, interval: int, count: int, output_mode: str):
        self.interval = interval
        self.count = count
        self.output_mode = output_mode
        self.sensors = {
            'furnace': self._generate_furnace_data,
            'rolling_mill': self._generate_rolling_mill_data,
            'conveyor': self._generate_conveyor_data,
            'packaging': self._generate_packaging_data
        }
        self.data_buffer = []
        
    def _add_noise(self, value: float, noise_factor: float = 0.05) -> float:
        """Add realistic noise to sensor readings"""
        return value * (1 + random.uniform(-noise_factor, noise_factor))
    
    def _generate_furnace_data(self, timestamp: datetime) -> Dict[str, Any]:
        """Generate furnace sensor data with realistic patterns"""
        base_temp = 1500 + 100 * np.sin(timestamp.hour / 24 * 2 * np.pi)
        
        return {
            'deviceId': f'furnace-{random.randint(1, 3)}',
            'timestamp': timestamp.isoformat(),
            'temperature': self._add_noise(base_temp),
            'pressure': self._add_noise(2.5),
            'fuelFlow': self._add_noise(150),
            'oxygenMix': self._add_noise(21.5),
            'powerDraw': self._add_noise(2500),
            'vibration': self._add_noise(0.05),
            'sensorType': 'furnace'
        }
    
    def _generate_rolling_mill_data(self, timestamp: datetime) -> Dict[str, Any]:
        """Generate rolling mill sensor data"""
        base_torque = 5000 + 500 * random.random()
        
        return {
            'deviceId': f'rolling-mill-{random.randint(1, 4)}',
            'timestamp': timestamp.isoformat(),
            'motorTorque': self._add_noise(base_torque),
            'motorCurrent': self._add_noise(base_torque / 50),
            'vibrationX': self._add_noise(0.1),
            'vibrationY': self._add_noise(0.1),
            'vibrationZ': self._add_noise(0.15),
            'rollGap': self._add_noise(5.0),
            'rollPressure': self._add_noise(1000),
            'temperature': self._add_noise(80),
            'speed': self._add_noise(10),
            'sensorType': 'rolling_mill'
        }
    
    def _generate_conveyor_data(self, timestamp: datetime) -> Dict[str, Any]:
        """Generate conveyor sensor data"""
        return {
            'deviceId': f'conveyor-{random.randint(1, 6)}',
            'timestamp': timestamp.isoformat(),
            'speed': self._add_noise(2.5),
            'vibration': self._add_noise(0.08),
            'temperature': self._add_noise(45),
            'motorCurrent': self._add_noise(30),
            'beltTension': self._add_noise(100),
            'loadWeight': self._add_noise(1000),
            'sensorType': 'conveyor'
        }
    
    def _generate_packaging_data(self, timestamp: datetime) -> Dict[str, Any]:
        """Generate packaging line sensor data"""
        return {
            'deviceId': f'packaging-{random.randint(1, 2)}',
            'timestamp': timestamp.isoformat(),
            'packagesPerMinute': random.randint(40, 60),
            'rejectRate': random.uniform(0, 0.05),
            'sealTemperature': self._add_noise(180),
            'conveyorSpeed': self._add_noise(1.5),
            'gpsLat': 40.7128 + random.uniform(-0.001, 0.001),
            'gpsLon': -74.0060 + random.uniform(-0.001, 0.001),
            'sensorType': 'packaging'
        }
    
    async def generate_data(self):
        """Main data generation loop"""
        current_time = datetime.utcnow()
        
        for i in range(self.count):
            for sensor_type, generator in self.sensors.items():
                data = generator(current_time)
                
                if random.random() < 0.05:
                    data = self._inject_anomaly(data, sensor_type)
                
                self.data_buffer.append(data)
            
            current_time += timedelta(seconds=self.interval)
            
            if self.output_mode == 'csv':
                await self._write_to_csv()
            else:
                await self._send_to_eventhub()
            
            await asyncio.sleep(self.interval)
    
    def _inject_anomaly(self, data: Dict[str, Any], sensor_type: str) -> Dict[str, Any]:
        """Inject anomalies for ML training"""
        if sensor_type == 'furnace':
            data['temperature'] *= random.choice([1.2, 0.8])
            data['anomaly'] = True
        elif sensor_type == 'rolling_mill':
            data['vibrationZ'] *= 3.0
            data['anomaly'] = True
        elif sensor_type == 'conveyor':
            data['vibration'] *= 2.5
            data['anomaly'] = True
        
        return data
    
    async def _write_to_csv(self):
        """Write buffered data to CSV file"""
        if not self.data_buffer:
            return
            
        file_exists = False
        try:
            with open('sample_signals.csv', 'r'):
                file_exists = True
        except FileNotFoundError:
            pass
        
        with open('sample_signals.csv', 'a', newline='') as csvfile:
            if self.data_buffer:
                fieldnames = self.data_buffer[0].keys()
                writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
                
                if not file_exists:
                    writer.writeheader()
                
                writer.writerows(self.data_buffer)
        
        print(f"Written {len(self.data_buffer)} records to sample_signals.csv")
        self.data_buffer = []
    
    async def _send_to_eventhub(self):
        """Send buffered data to Azure Event Hub"""
        if not self.data_buffer:
            return
            
        connection_string = os.environ.get('EVENTHUB_CONNECTION_STRING', '')
        eventhub_name = os.environ.get('EVENTHUB_NAME', 'steel-mill-telemetry')
        
        if not connection_string:
            print("Warning: EVENTHUB_CONNECTION_STRING not set. Writing to CSV instead.")
            await self._write_to_csv()
            return
        
        async with EventHubProducerClient.from_connection_string(
            connection_string, eventhub_name=eventhub_name
        ) as producer:
            
            event_data_batch = await producer.create_batch()
            
            for data in self.data_buffer:
                event = EventData(json.dumps(data))
                try:
                    event_data_batch.add(event)
                except ValueError:
                    await producer.send_batch(event_data_batch)
                    event_data_batch = await producer.create_batch()
                    event_data_batch.add(event)
            
            await producer.send_batch(event_data_batch)
        
        print(f"Sent {len(self.data_buffer)} events to Event Hub")
        self.data_buffer = []

async def main():
    parser = argparse.ArgumentParser(description='Steel Mill Sensor Simulator')
    parser.add_argument('--interval', type=int, default=5, 
                       help='Interval between readings in seconds (default: 5)')
    parser.add_argument('--count', type=int, default=100,
                       help='Number of readings to generate (default: 100)')
    parser.add_argument('--output', choices=['csv', 'eventhub'], default='csv',
                       help='Output mode: csv or eventhub (default: csv)')
    
    args = parser.parse_args()
    
    print(f"Starting sensor simulation...")
    print(f"Interval: {args.interval}s, Count: {args.count}, Output: {args.output}")
    
    simulator = SensorSimulator(args.interval, args.count, args.output)
    await simulator.generate_data()
    
    print("Simulation complete!")

if __name__ == '__main__':
    import os
    asyncio.run(main())