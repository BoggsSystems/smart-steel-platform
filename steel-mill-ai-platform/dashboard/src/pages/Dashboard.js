import React, { useState, useEffect } from 'react';
import {
  Box,
  Grid,
  Card,
  CardContent,
  Typography,
  LinearProgress,
  Chip,
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
  PieChart,
  Pie,
  Cell,
} from 'recharts';

// Mock data - in real implementation, this would come from APIs
const mockTelemetryData = [
  { time: '08:00', temperature: 1485, vibration: 0.05, energy: 2450 },
  { time: '08:30', temperature: 1492, vibration: 0.06, energy: 2520 },
  { time: '09:00', temperature: 1498, vibration: 0.04, energy: 2380 },
  { time: '09:30', temperature: 1503, vibration: 0.07, energy: 2610 },
  { time: '10:00', temperature: 1489, vibration: 0.05, energy: 2490 },
  { time: '10:30', temperature: 1495, vibration: 0.06, energy: 2550 },
];

const mockDeviceStatus = [
  { name: 'Operational', value: 12, color: '#4caf50' },
  { name: 'Maintenance', value: 2, color: '#ff9800' },
  { name: 'Offline', value: 1, color: '#f44336' },
];

const mockAlerts = [
  { id: 1, type: 'warning', device: 'Rolling Mill 2', message: 'High vibration detected', time: '10:25 AM' },
  { id: 2, type: 'info', device: 'Furnace 1', message: 'Scheduled maintenance in 2 days', time: '09:45 AM' },
  { id: 3, type: 'error', device: 'Conveyor 3', message: 'Motor temperature critical', time: '08:30 AM' },
];

function StatCard({ title, value, unit, change, color = 'primary' }) {
  return (
    <Card elevation={2}>
      <CardContent>
        <Typography color="textSecondary" gutterBottom variant="body2">
          {title}
        </Typography>
        <Typography variant="h4" component="div">
          {value} <Typography variant="h6" component="span" color="textSecondary">{unit}</Typography>
        </Typography>
        {change && (
          <Chip
            label={`${change > 0 ? '+' : ''}${change}%`}
            color={change > 0 ? 'success' : 'error'}
            size="small"
            sx={{ mt: 1 }}
          />
        )}
      </CardContent>
    </Card>
  );
}

function Dashboard() {
  const [currentTime, setCurrentTime] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Production Dashboard
      </Typography>
      
      <Typography variant="body1" color="textSecondary" gutterBottom>
        {currentTime.toLocaleString()}
      </Typography>

      <Grid container spacing={3}>
        {/* Key Metrics */}
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Active Devices"
            value="15"
            unit="units"
            change={2.5}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Production Rate"
            value="850"
            unit="tons/day"
            change={-1.2}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Energy Efficiency"
            value="94.2"
            unit="%"
            change={3.1}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Quality Score"
            value="96.8"
            unit="%"
            change={0.8}
          />
        </Grid>

        {/* Real-time Telemetry */}
        <Grid item xs={12} lg={8}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Real-time Telemetry
              </Typography>
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={mockTelemetryData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="time" />
                  <YAxis yAxisId="left" />
                  <YAxis yAxisId="right" orientation="right" />
                  <Tooltip />
                  <Line
                    yAxisId="left"
                    type="monotone"
                    dataKey="temperature"
                    stroke="#ff6b6b"
                    strokeWidth={2}
                    name="Temperature (°C)"
                  />
                  <Line
                    yAxisId="right"
                    type="monotone"
                    dataKey="energy"
                    stroke="#4ecdc4"
                    strokeWidth={2}
                    name="Energy (kW)"
                  />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Device Status */}
        <Grid item xs={12} lg={4}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Device Status
              </Typography>
              <ResponsiveContainer width="100%" height={250}>
                <PieChart>
                  <Pie
                    data={mockDeviceStatus}
                    cx="50%"
                    cy="50%"
                    innerRadius={60}
                    outerRadius={100}
                    paddingAngle={5}
                    dataKey="value"
                  >
                    {mockDeviceStatus.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
              <Box sx={{ mt: 2 }}>
                {mockDeviceStatus.map((status, index) => (
                  <Chip
                    key={index}
                    label={`${status.name}: ${status.value}`}
                    sx={{ 
                      mr: 1, 
                      mb: 1,
                      backgroundColor: status.color + '20',
                      color: status.color,
                      border: `1px solid ${status.color}`,
                    }}
                    variant="outlined"
                  />
                ))}
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* Recent Alerts */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Recent Alerts
              </Typography>
              <Box>
                {mockAlerts.map((alert) => (
                  <Alert
                    key={alert.id}
                    severity={alert.type}
                    sx={{ mb: 1 }}
                    action={
                      <Typography variant="caption" color="textSecondary">
                        {alert.time}
                      </Typography>
                    }
                  >
                    <strong>{alert.device}:</strong> {alert.message}
                  </Alert>
                ))}
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* ML Model Performance */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                ML Model Performance
              </Typography>
              <Grid container spacing={2}>
                <Grid item xs={12} md={4}>
                  <Box sx={{ p: 2, textAlign: 'center' }}>
                    <Typography variant="h5" color="success.main">98.5%</Typography>
                    <Typography variant="body2" color="textSecondary">Predictive Maintenance</Typography>
                    <LinearProgress variant="determinate" value={98.5} sx={{ mt: 1 }} />
                  </Box>
                </Grid>
                <Grid item xs={12} md={4}>
                  <Box sx={{ p: 2, textAlign: 'center' }}>
                    <Typography variant="h5" color="info.main">96.2%</Typography>
                    <Typography variant="body2" color="textSecondary">Anomaly Detection</Typography>
                    <LinearProgress variant="determinate" value={96.2} color="info" sx={{ mt: 1 }} />
                  </Box>
                </Grid>
                <Grid item xs={12} md={4}>
                  <Box sx={{ p: 2, textAlign: 'center' }}>
                    <Typography variant="h5" color="warning.main">94.8%</Typography>
                    <Typography variant="body2" color="textSecondary">Energy Optimization</Typography>
                    <LinearProgress variant="determinate" value={94.8} color="warning" sx={{ mt: 1 }} />
                  </Box>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}

export default Dashboard;