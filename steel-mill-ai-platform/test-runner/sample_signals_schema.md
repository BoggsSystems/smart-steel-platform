# Sample Signals CSV Schema

## Overview
The `sample_signals.csv` file contains simulated sensor data from various steel mill equipment. Each row represents a single sensor reading at a specific timestamp.

## Common Fields (All Sensor Types)

| Field | Type | Description | Example |
|-------|------|-------------|---------|
| deviceId | string | Unique identifier for the device | furnace-1 |
| timestamp | ISO 8601 | UTC timestamp of the reading | 2024-01-15T10:30:00Z |
| sensorType | string | Type of sensor/equipment | furnace |

## Sensor-Specific Fields

### Furnace Sensors (`sensorType: furnace`)

| Field | Type | Unit | Range | Description |
|-------|------|------|-------|-------------|
| temperature | float | °C | 1400-1600 | Current furnace temperature |
| pressure | float | bar | 2.0-3.0 | Internal pressure |
| fuelFlow | float | L/min | 100-200 | Fuel consumption rate |
| oxygenMix | float | % | 20-23 | Oxygen concentration |
| powerDraw | float | kW | 2000-3000 | Electrical power consumption |
| vibration | float | g | 0.01-0.2 | Vibration intensity |

### Rolling Mill Sensors (`sensorType: rolling_mill`)

| Field | Type | Unit | Range | Description |
|-------|------|------|-------|-------------|
| motorTorque | float | Nm | 4000-6000 | Motor torque |
| motorCurrent | float | A | 80-120 | Motor current draw |
| vibrationX | float | g | 0.05-0.2 | X-axis vibration |
| vibrationY | float | g | 0.05-0.2 | Y-axis vibration |
| vibrationZ | float | g | 0.05-0.3 | Z-axis vibration |
| rollGap | float | mm | 3-10 | Gap between rolls |
| rollPressure | float | MPa | 800-1200 | Rolling pressure |
| temperature | float | °C | 60-100 | Roll temperature |
| speed | float | m/s | 5-15 | Rolling speed |

### Conveyor Sensors (`sensorType: conveyor`)

| Field | Type | Unit | Range | Description |
|-------|------|------|-------|-------------|
| speed | float | m/s | 1-5 | Belt speed |
| vibration | float | g | 0.01-0.15 | Belt vibration |
| temperature | float | °C | 30-60 | Motor temperature |
| motorCurrent | float | A | 20-50 | Motor current |
| beltTension | float | N | 80-120 | Belt tension |
| loadWeight | float | kg | 500-2000 | Current load weight |

### Packaging Line Sensors (`sensorType: packaging`)

| Field | Type | Unit | Range | Description |
|-------|------|------|-------|-------------|
| packagesPerMinute | int | count | 30-70 | Packaging rate |
| rejectRate | float | % | 0-10 | Package rejection rate |
| sealTemperature | float | °C | 160-200 | Heat sealer temperature |
| conveyorSpeed | float | m/s | 0.5-3 | Conveyor speed |
| gpsLat | float | degrees | -90 to 90 | GPS latitude (for shipments) |
| gpsLon | float | degrees | -180 to 180 | GPS longitude |

## Special Fields

| Field | Type | Description |
|-------|------|-------------|
| anomaly | boolean | True if reading contains injected anomaly (optional) |

## Sample Data

```csv
deviceId,timestamp,sensorType,temperature,pressure,fuelFlow,oxygenMix,powerDraw,vibration
furnace-1,2024-01-15T10:30:00Z,furnace,1485.3,2.45,145.2,21.3,2456.8,0.052
furnace-2,2024-01-15T10:30:00Z,furnace,1502.1,2.51,148.9,21.5,2489.3,0.048
rolling-mill-1,2024-01-15T10:30:05Z,rolling_mill,82.5,,,,,0.095
```

## Data Generation Patterns

### Normal Operating Conditions
- **Temperature**: Follows sinusoidal pattern based on time of day
- **Vibration**: Random noise around baseline with occasional spikes
- **Power Draw**: Correlates with production load and time of day

### Anomaly Patterns
- **High Temperature**: 20% above normal range
- **Vibration Spikes**: 2-3x normal vibration levels
- **Power Surges**: Sudden increases in power draw

## Usage Notes

1. **Timestamp Resolution**: 5-second intervals by default
2. **Missing Values**: Represented as empty strings in CSV
3. **Data Volume**: ~17,280 readings per device per day (5-sec intervals)
4. **File Size**: ~10MB per day for 20 devices

## ML Training Considerations

- **Feature Engineering**: Rolling statistics, time-based features
- **Label Generation**: Synthetic labels based on threshold rules
- **Train/Test Split**: Chronological split recommended
- **Data Imbalance**: ~5% anomalies, ~10% maintenance events