// MongoDB initialization script for Steel Mill AI Platform
// This script creates databases, collections, and indexes for local development

// Switch to the steel mill database
db = db.getSiblingDB('steelmill');

// Create collections with validation schemas
db.createCollection('furnace_data', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['furnaceId', 'timestamp', 'temperature', 'powerConsumption'],
      properties: {
        furnaceId: { bsonType: 'string' },
        timestamp: { bsonType: 'date' },
        temperature: { bsonType: 'double', minimum: 0, maximum: 2000 },
        powerConsumption: { bsonType: 'double', minimum: 0 },
        efficiency: { bsonType: 'double', minimum: 0, maximum: 100 },
        status: { enum: ['operational', 'maintenance', 'shutdown', 'startup'] }
      }
    }
  }
});

db.createCollection('quality_tests', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['sampleId', 'testType', 'timestamp', 'result'],
      properties: {
        sampleId: { bsonType: 'string' },
        testType: { enum: ['tensile', 'bend', 'dimensional', 'chemical'] },
        timestamp: { bsonType: 'date' },
        result: { bsonType: 'object' },
        grade: { enum: ['Grade40', 'Grade60', 'Grade75', 'Grade80'] },
        passed: { bsonType: 'bool' }
      }
    }
  }
});

db.createCollection('maintenance_records', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['equipmentId', 'timestamp', 'maintenanceType'],
      properties: {
        equipmentId: { bsonType: 'string' },
        timestamp: { bsonType: 'date' },
        maintenanceType: { enum: ['preventive', 'corrective', 'predictive', 'emergency'] },
        status: { enum: ['scheduled', 'in_progress', 'completed', 'cancelled'] },
        cost: { bsonType: 'double', minimum: 0 },
        description: { bsonType: 'string' }
      }
    }
  }
});

db.createCollection('production_batches', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['batchId', 'startTime', 'grade', 'targetQuantity'],
      properties: {
        batchId: { bsonType: 'string' },
        startTime: { bsonType: 'date' },
        endTime: { bsonType: 'date' },
        grade: { enum: ['Grade40', 'Grade60', 'Grade75', 'Grade80'] },
        targetQuantity: { bsonType: 'int', minimum: 1 },
        actualQuantity: { bsonType: 'int', minimum: 0 },
        status: { enum: ['planning', 'in_progress', 'completed', 'cancelled'] }
      }
    }
  }
});

// Create indexes for performance
db.furnace_data.createIndex({ 'furnaceId': 1, 'timestamp': -1 });
db.furnace_data.createIndex({ 'timestamp': -1 });

db.quality_tests.createIndex({ 'sampleId': 1 });
db.quality_tests.createIndex({ 'timestamp': -1 });
db.quality_tests.createIndex({ 'testType': 1, 'timestamp': -1 });

db.maintenance_records.createIndex({ 'equipmentId': 1, 'timestamp': -1 });
db.maintenance_records.createIndex({ 'status': 1 });
db.maintenance_records.createIndex({ 'maintenanceType': 1 });

db.production_batches.createIndex({ 'batchId': 1 });
db.production_batches.createIndex({ 'startTime': -1 });
db.production_batches.createIndex({ 'status': 1 });

// Insert sample data for development
db.furnace_data.insertMany([
  {
    furnaceId: 'EAF-001',
    timestamp: new Date(),
    temperature: 1650.5,
    powerConsumption: 85.2,
    efficiency: 92.1,
    status: 'operational',
    electrodePosition: 150.2,
    arcVoltage: 480.5
  },
  {
    furnaceId: 'EAF-002',
    timestamp: new Date(),
    temperature: 1520.8,
    powerConsumption: 78.9,
    efficiency: 89.7,
    status: 'operational',
    electrodePosition: 145.8,
    arcVoltage: 465.2
  }
]);

db.quality_tests.insertMany([
  {
    sampleId: 'QC-2024-001',
    testType: 'tensile',
    timestamp: new Date(),
    grade: 'Grade60',
    result: {
      yieldStrength: 445.2,
      tensileStrength: 665.8,
      elongation: 12.5,
      reductionOfArea: 58.2
    },
    passed: true,
    testOperator: 'John Smith',
    equipmentUsed: 'Tensile-Test-001'
  },
  {
    sampleId: 'QC-2024-002',
    testType: 'bend',
    timestamp: new Date(),
    grade: 'Grade60',
    result: {
      bendAngle: 180,
      crackObserved: false,
      bendRadius: 3.0
    },
    passed: true,
    testOperator: 'Jane Doe',
    equipmentUsed: 'Bend-Test-001'
  }
]);

db.maintenance_records.insertMany([
  {
    equipmentId: 'EAF-001',
    timestamp: new Date(),
    maintenanceType: 'preventive',
    status: 'completed',
    cost: 15000.0,
    description: 'Electrode replacement and refractory repair',
    technician: 'Mike Johnson',
    duration: 8.5
  },
  {
    equipmentId: 'ROLLING-MILL-001',
    timestamp: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000), // 7 days from now
    maintenanceType: 'preventive',
    status: 'scheduled',
    estimatedCost: 8000.0,
    description: 'Roll change and bearing inspection',
    plannedDuration: 6.0
  }
]);

db.production_batches.insertMany([
  {
    batchId: 'BATCH-2024-001',
    startTime: new Date(Date.now() - 2 * 60 * 60 * 1000), // 2 hours ago
    grade: 'Grade60',
    targetQuantity: 50000,
    actualQuantity: 48500,
    status: 'in_progress',
    heatNumber: 'H240101',
    customerOrder: 'CO-2024-0015'
  },
  {
    batchId: 'BATCH-2024-002',
    startTime: new Date(Date.now() - 24 * 60 * 60 * 1000), // 24 hours ago
    endTime: new Date(Date.now() - 18 * 60 * 60 * 1000), // 18 hours ago
    grade: 'Grade40',
    targetQuantity: 30000,
    actualQuantity: 30200,
    status: 'completed',
    heatNumber: 'H240102',
    customerOrder: 'CO-2024-0016',
    qualityScore: 96.5
  }
]);

// Create user for application access
db.createUser({
  user: 'steelmill-app',
  pwd: 'app123',
  roles: [
    {
      role: 'readWrite',
      db: 'steelmill'
    }
  ]
});

print('MongoDB initialization completed for Steel Mill AI Platform');
print('Created collections: furnace_data, quality_tests, maintenance_records, production_batches');
print('Created indexes for performance optimization');
print('Inserted sample data for development');
print('Created application user: steelmill-app');