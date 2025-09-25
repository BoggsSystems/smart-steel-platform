import React, { useState, useEffect } from 'react';
import {
  Box,
  Grid,
  Card,
  CardContent,
  Typography,
  LinearProgress,
  Chip,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Button,
  Alert,
} from '@mui/material';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  BarChart,
  Bar,
  AreaChart,
  Area,
  ScatterChart,
  Scatter,
} from 'recharts';

// Mock data for ML model performance
const mockModelAccuracy = [
  { date: '2024-01-01', predictiveMaintenance: 98.5, anomalyDetection: 96.2, energyOptimization: 94.8 },
  { date: '2024-01-02', predictiveMaintenance: 98.1, anomalyDetection: 96.8, energyOptimization: 95.1 },
  { date: '2024-01-03', predictiveMaintenance: 97.9, anomalyDetection: 95.9, energyOptimization: 94.3 },
  { date: '2024-01-04', predictiveMaintenance: 98.3, anomalyDetection: 96.5, energyOptimization: 95.2 },
  { date: '2024-01-05', predictiveMaintenance: 98.7, anomalyDetection: 97.1, energyOptimization: 95.6 },
  { date: '2024-01-06', predictiveMaintenance: 98.2, anomalyDetection: 96.7, energyOptimization: 94.9 },
  { date: '2024-01-07', predictiveMaintenance: 98.9, anomalyDetection: 97.3, energyOptimization: 95.8 },
];

const mockDriftData = [
  { feature: 'temperature', current: 0.02, baseline: 0.01, threshold: 0.05, status: 'normal' },
  { feature: 'pressure', current: 0.08, baseline: 0.03, threshold: 0.1, status: 'warning' },
  { feature: 'vibration', current: 0.12, baseline: 0.04, threshold: 0.1, status: 'critical' },
  { feature: 'energy_consumption', current: 0.03, baseline: 0.02, threshold: 0.06, status: 'normal' },
  { feature: 'throughput', current: 0.07, baseline: 0.05, threshold: 0.09, status: 'warning' },
];

const mockModelVersions = [
  { 
    id: 'v2.3.1', 
    model: 'Predictive Maintenance', 
    deployed: '2024-01-05', 
    accuracy: 98.9, 
    status: 'active',
    description: 'Enhanced bearing failure prediction with new sensor data'
  },
  { 
    id: 'v1.8.2', 
    model: 'Anomaly Detection', 
    deployed: '2024-01-03', 
    accuracy: 97.3, 
    status: 'active',
    description: 'Improved false positive reduction in temperature anomalies'
  },
  { 
    id: 'v3.1.0', 
    model: 'Energy Optimization', 
    deployed: '2024-01-04', 
    accuracy: 95.8, 
    status: 'active',
    description: 'Multi-furnace optimization with dynamic load balancing'
  },
  { 
    id: 'v2.2.8', 
    model: 'Predictive Maintenance', 
    deployed: '2023-12-28', 
    accuracy: 98.1, 
    status: 'retired',
    description: 'Previous version with basic vibration analysis'
  },
];

const mockInferenceData = [
  { hour: '00:00', predictions: 1240, errors: 2, avgLatency: 45 },
  { hour: '04:00', predictions: 980, errors: 1, avgLatency: 42 },
  { hour: '08:00', predictions: 1850, errors: 5, avgLatency: 48 },
  { hour: '12:00', predictions: 2100, errors: 3, avgLatency: 46 },
  { hour: '16:00', predictions: 1950, errors: 4, avgLatency: 50 },
  { hour: '20:00', predictions: 1650, errors: 2, avgLatency: 44 },
];

function ModelCard({ title, accuracy, change, version, status }) {
  const getStatusColor = (status) => {
    switch (status) {
      case 'active': return 'success';
      case 'training': return 'info';
      case 'error': return 'error';
      default: return 'default';
    }
  };

  return (
    <Card elevation={2}>
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
          <Typography variant="h6" component="div" sx={{ fontSize: '1rem' }}>
            {title}
          </Typography>
          <Chip
            label={status}
            color={getStatusColor(status)}
            size="small"
          />
        </Box>
        
        <Typography variant="h3" component="div" sx={{ mb: 1 }}>
          {accuracy}%
        </Typography>
        
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Chip
            label={`${change > 0 ? '+' : ''}${change}%`}
            color={change > 0 ? 'success' : 'error'}
            size="small"
          />
          <Typography variant="caption" color="textSecondary">
            {version}
          </Typography>
        </Box>
        
        <LinearProgress 
          variant="determinate" 
          value={accuracy} 
          sx={{ mt: 2, height: 6, borderRadius: 3 }} 
        />
      </CardContent>
    </Card>
  );
}

function DriftIndicator({ feature, current, baseline, threshold, status }) {
  const getStatusColor = (status) => {
    switch (status) {
      case 'normal': return '#4caf50';
      case 'warning': return '#ff9800';
      case 'critical': return '#f44336';
      default: return '#757575';
    }
  };

  const driftPercentage = ((current - baseline) / baseline * 100).toFixed(1);

  return (
    <Box sx={{ mb: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
        <Typography variant="body2" sx={{ textTransform: 'capitalize' }}>
          {feature.replace('_', ' ')}
        </Typography>
        <Chip
          label={status.toUpperCase()}
          size="small"
          sx={{
            backgroundColor: getStatusColor(status) + '20',
            color: getStatusColor(status),
            border: `1px solid ${getStatusColor(status)}`,
            fontWeight: 'bold'
          }}
        />
      </Box>
      
      <LinearProgress
        variant="determinate"
        value={(current / threshold) * 100}
        sx={{
          height: 8,
          borderRadius: 4,
          backgroundColor: '#e0e0e0',
          '& .MuiLinearProgress-bar': {
            backgroundColor: getStatusColor(status),
            borderRadius: 4,
          }
        }}
      />
      
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 1 }}>
        <Typography variant="caption" color="textSecondary">
          Current: {current.toFixed(3)}
        </Typography>
        <Typography variant="caption" color="textSecondary">
          Drift: {driftPercentage}%
        </Typography>
        <Typography variant="caption" color="textSecondary">
          Threshold: {threshold.toFixed(3)}
        </Typography>
      </Box>
    </Box>
  );
}

function ModelMonitoring() {
  const [selectedModel, setSelectedModel] = useState('all');
  const [timeRange, setTimeRange] = useState('7d');

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        ML Model Monitoring
      </Typography>
      
      <Typography variant="body1" color="textSecondary" gutterBottom sx={{ mb: 3 }}>
        Monitor machine learning model performance, detect data drift, and manage model versions
      </Typography>

      {/* Controls */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={6} md={3}>
          <FormControl fullWidth size="small">
            <InputLabel>Model</InputLabel>
            <Select
              value={selectedModel}
              onChange={(e) => setSelectedModel(e.target.value)}
              label="Model"
            >
              <MenuItem value="all">All Models</MenuItem>
              <MenuItem value="predictive">Predictive Maintenance</MenuItem>
              <MenuItem value="anomaly">Anomaly Detection</MenuItem>
              <MenuItem value="energy">Energy Optimization</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        
        <Grid item xs={12} sm={6} md={3}>
          <FormControl fullWidth size="small">
            <InputLabel>Time Range</InputLabel>
            <Select
              value={timeRange}
              onChange={(e) => setTimeRange(e.target.value)}
              label="Time Range"
            >
              <MenuItem value="1d">Last 24 Hours</MenuItem>
              <MenuItem value="7d">Last 7 Days</MenuItem>
              <MenuItem value="30d">Last 30 Days</MenuItem>
              <MenuItem value="90d">Last 90 Days</MenuItem>
            </Select>
          </FormControl>
        </Grid>
      </Grid>

      <Grid container spacing={3}>
        {/* Model Performance Cards */}
        <Grid item xs={12} sm={6} lg={4}>
          <ModelCard
            title="Predictive Maintenance"
            accuracy={98.9}
            change={0.7}
            version="v2.3.1"
            status="active"
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={4}>
          <ModelCard
            title="Anomaly Detection"
            accuracy={97.3}
            change={0.2}
            version="v1.8.2"
            status="active"
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={4}>
          <ModelCard
            title="Energy Optimization"
            accuracy={95.8}
            change={0.9}
            version="v3.1.0"
            status="active"
          />
        </Grid>

        {/* Model Accuracy Trend */}
        <Grid item xs={12} lg={8}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Model Accuracy Trend
              </Typography>
              <ResponsiveContainer width="100%" height={350}>
                <LineChart data={mockModelAccuracy}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" />
                  <YAxis domain={[90, 100]} />
                  <Tooltip formatter={(value, name) => [`${value}%`, name]} />
                  <Line
                    type="monotone"
                    dataKey="predictiveMaintenance"
                    stroke="#4caf50"
                    strokeWidth={2}
                    name="Predictive Maintenance"
                  />
                  <Line
                    type="monotone"
                    dataKey="anomalyDetection"
                    stroke="#2196f3"
                    strokeWidth={2}
                    name="Anomaly Detection"
                  />
                  <Line
                    type="monotone"
                    dataKey="energyOptimization"
                    stroke="#ff9800"
                    strokeWidth={2}
                    name="Energy Optimization"
                  />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Data Drift Detection */}
        <Grid item xs={12} lg={4}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Data Drift Detection
              </Typography>
              <Box sx={{ mt: 2 }}>
                {mockDriftData.map((drift, index) => (
                  <DriftIndicator
                    key={index}
                    feature={drift.feature}
                    current={drift.current}
                    baseline={drift.baseline}
                    threshold={drift.threshold}
                    status={drift.status}
                  />
                ))}
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Inference Statistics */}
        <Grid item xs={12} lg={6}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Daily Inference Statistics
              </Typography>
              <ResponsiveContainer width="100%" height={280}>
                <AreaChart data={mockInferenceData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="hour" />
                  <YAxis />
                  <Tooltip />
                  <Area
                    type="monotone"
                    dataKey="predictions"
                    stackId="1"
                    stroke="#4caf50"
                    fill="#4caf50"
                    fillOpacity={0.3}
                    name="Predictions"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Error Rate and Latency */}
        <Grid item xs={12} lg={6}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Error Rate & Latency
              </Typography>
              <ResponsiveContainer width="100%" height={280}>
                <BarChart data={mockInferenceData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="hour" />
                  <YAxis yAxisId="left" />
                  <YAxis yAxisId="right" orientation="right" />
                  <Tooltip />
                  <Bar
                    yAxisId="left"
                    dataKey="errors"
                    fill="#f44336"
                    name="Errors"
                  />
                  <Bar
                    yAxisId="right"
                    dataKey="avgLatency"
                    fill="#2196f3"
                    name="Avg Latency (ms)"
                  />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Model Versions */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Model Version Management
              </Typography>
              <TableContainer component={Paper} elevation={0}>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>Version</TableCell>
                      <TableCell>Model</TableCell>
                      <TableCell>Deployed</TableCell>
                      <TableCell>Accuracy</TableCell>
                      <TableCell>Status</TableCell>
                      <TableCell>Description</TableCell>
                      <TableCell>Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {mockModelVersions.map((version) => (
                      <TableRow key={version.id}>
                        <TableCell>
                          <Typography variant="body2" fontWeight="bold">
                            {version.id}
                          </Typography>
                        </TableCell>
                        <TableCell>{version.model}</TableCell>
                        <TableCell>{version.deployed}</TableCell>
                        <TableCell>
                          <Typography
                            color={version.accuracy > 97 ? 'success.main' : 'text.primary'}
                            fontWeight="bold"
                          >
                            {version.accuracy}%
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={version.status}
                            color={version.status === 'active' ? 'success' : 'default'}
                            size="small"
                          />
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" sx={{ maxWidth: 300 }}>
                            {version.description}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <Button
                            size="small"
                            variant={version.status === 'active' ? 'outlined' : 'contained'}
                            color="primary"
                            disabled={version.status === 'active'}
                          >
                            {version.status === 'active' ? 'Active' : 'Deploy'}
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Alerts */}
        <Grid item xs={12}>
          <Alert severity="warning" sx={{ mb: 2 }}>
            <Typography variant="body2">
              <strong>Data Drift Alert:</strong> Vibration feature showing critical drift (0.12 vs 0.04 baseline). 
              Model retraining recommended within 24 hours.
            </Typography>
          </Alert>
        </Grid>
      </Grid>
    </Box>
  );
}

export default ModelMonitoring;