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
  IconButton,
  Avatar,
  Badge,
  TextField,
  InputAdornment,
} from '@mui/material';
import {
  Search as SearchIcon,
  Refresh as RefreshIcon,
  Settings as SettingsIcon,
  Warning as WarningIcon,
  CheckCircle as CheckCircleIcon,
  Error as ErrorIcon,
  Pause as PauseIcon,
} from '@mui/icons-material';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  AreaChart,
  Area,
  RadialBarChart,
  RadialBar,
  PieChart,
  Pie,
  Cell,
} from 'recharts';

// Mock data for device status
const mockDevices = [
  {
    id: 'FURN-001',
    name: 'Electric Arc Furnace 1',
    type: 'Furnace',
    status: 'operational',
    location: 'Zone A',
    temperature: 1650,
    power: 85.2,
    efficiency: 94.8,
    lastMaintenance: '2024-01-15',
    nextMaintenance: '2024-02-15',
    uptime: 98.5,
    alerts: 0
  },
  {
    id: 'ROLL-002',
    name: 'Rolling Mill 2',
    type: 'Rolling Mill',
    status: 'warning',
    location: 'Zone B',
    temperature: 180,
    power: 67.4,
    efficiency: 89.2,
    lastMaintenance: '2024-01-10',
    nextMaintenance: '2024-01-25',
    uptime: 96.2,
    alerts: 2
  },
  {
    id: 'CONV-003',
    name: 'Conveyor System 3',
    type: 'Conveyor',
    status: 'critical',
    location: 'Zone C',
    temperature: 45,
    power: 12.8,
    efficiency: 72.1,
    lastMaintenance: '2023-12-20',
    nextMaintenance: '2024-01-20',
    uptime: 84.7,
    alerts: 5
  },
  {
    id: 'CAST-004',
    name: 'Continuous Caster 4',
    type: 'Caster',
    status: 'operational',
    location: 'Zone D',
    temperature: 1200,
    power: 72.3,
    efficiency: 92.6,
    lastMaintenance: '2024-01-18',
    nextMaintenance: '2024-03-18',
    uptime: 97.8,
    alerts: 0
  },
  {
    id: 'PUMP-005',
    name: 'Cooling Water Pump 5',
    type: 'Pump',
    status: 'maintenance',
    location: 'Zone A',
    temperature: 35,
    power: 0,
    efficiency: 0,
    lastMaintenance: '2024-01-20',
    nextMaintenance: '2024-02-20',
    uptime: 99.1,
    alerts: 1
  },
  {
    id: 'COMP-006',
    name: 'Air Compressor 6',
    type: 'Compressor',
    status: 'operational',
    location: 'Zone B',
    temperature: 75,
    power: 34.5,
    efficiency: 91.4,
    lastMaintenance: '2024-01-05',
    nextMaintenance: '2024-04-05',
    uptime: 98.9,
    alerts: 0
  }
];

const mockTelemetryHistory = [
  { time: '08:00', temperature: 1485, vibration: 0.05, pressure: 850 },
  { time: '09:00', temperature: 1498, vibration: 0.04, pressure: 855 },
  { time: '10:00', temperature: 1503, vibration: 0.07, pressure: 848 },
  { time: '11:00', temperature: 1489, vibration: 0.05, pressure: 852 },
  { time: '12:00', temperature: 1495, vibration: 0.06, pressure: 849 },
  { time: '13:00', temperature: 1501, vibration: 0.08, pressure: 847 },
  { time: '14:00', temperature: 1487, vibration: 0.04, pressure: 854 },
];

const mockStatusDistribution = [
  { name: 'Operational', value: 4, color: '#4caf50' },
  { name: 'Warning', value: 1, color: '#ff9800' },
  { name: 'Critical', value: 1, color: '#f44336' },
  { name: 'Maintenance', value: 1, color: '#9c27b0' },
];

const mockZoneData = [
  { zone: 'Zone A', devices: 3, operational: 2, issues: 1 },
  { zone: 'Zone B', devices: 2, operational: 1, issues: 1 },
  { zone: 'Zone C', devices: 1, operational: 0, issues: 1 },
  { zone: 'Zone D', devices: 1, operational: 1, issues: 0 },
];

function DeviceCard({ device }) {
  const getStatusIcon = (status) => {
    switch (status) {
      case 'operational':
        return <CheckCircleIcon sx={{ color: '#4caf50' }} />;
      case 'warning':
        return <WarningIcon sx={{ color: '#ff9800' }} />;
      case 'critical':
        return <ErrorIcon sx={{ color: '#f44336' }} />;
      case 'maintenance':
        return <PauseIcon sx={{ color: '#9c27b0' }} />;
      default:
        return <ErrorIcon sx={{ color: '#757575' }} />;
    }
  };

  const getStatusColor = (status) => {
    switch (status) {
      case 'operational': return 'success';
      case 'warning': return 'warning';
      case 'critical': return 'error';
      case 'maintenance': return 'info';
      default: return 'default';
    }
  };

  return (
    <Card elevation={2} sx={{ height: '100%' }}>
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Avatar sx={{ bgcolor: 'primary.main', width: 32, height: 32, fontSize: '0.75rem' }}>
              {device.type.charAt(0)}
            </Avatar>
            <Box>
              <Typography variant="subtitle2" noWrap>
                {device.name}
              </Typography>
              <Typography variant="caption" color="textSecondary">
                {device.id} • {device.location}
              </Typography>
            </Box>
          </Box>
          
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            {device.alerts > 0 && (
              <Badge badgeContent={device.alerts} color="error" sx={{ mr: 1 }}>
                <WarningIcon fontSize="small" />
              </Badge>
            )}
            {getStatusIcon(device.status)}
          </Box>
        </Box>

        <Box sx={{ mb: 2 }}>
          <Chip
            label={device.status.toUpperCase()}
            color={getStatusColor(device.status)}
            size="small"
            sx={{ mb: 1 }}
          />
        </Box>

        <Grid container spacing={2}>
          <Grid item xs={6}>
            <Typography variant="caption" color="textSecondary" display="block">
              Power Usage
            </Typography>
            <Typography variant="h6">
              {device.power}%
            </Typography>
            <LinearProgress 
              variant="determinate" 
              value={device.power} 
              sx={{ mt: 0.5, height: 4, borderRadius: 2 }}
            />
          </Grid>
          
          <Grid item xs={6}>
            <Typography variant="caption" color="textSecondary" display="block">
              Efficiency
            </Typography>
            <Typography variant="h6">
              {device.efficiency}%
            </Typography>
            <LinearProgress 
              variant="determinate" 
              value={device.efficiency} 
              color="success"
              sx={{ mt: 0.5, height: 4, borderRadius: 2 }}
            />
          </Grid>

          <Grid item xs={6}>
            <Typography variant="caption" color="textSecondary" display="block">
              Temperature
            </Typography>
            <Typography variant="body2">
              {device.temperature}°C
            </Typography>
          </Grid>
          
          <Grid item xs={6}>
            <Typography variant="caption" color="textSecondary" display="block">
              Uptime
            </Typography>
            <Typography variant="body2">
              {device.uptime}%
            </Typography>
          </Grid>
        </Grid>

        <Box sx={{ mt: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="caption" color="textSecondary">
            Next maintenance: {device.nextMaintenance}
          </Typography>
          <IconButton size="small">
            <SettingsIcon fontSize="small" />
          </IconButton>
        </Box>
      </CardContent>
    </Card>
  );
}

function StatusSummaryCard({ title, value, total, color, icon }) {
  const percentage = total > 0 ? (value / total) * 100 : 0;

  return (
    <Card elevation={2}>
      <CardContent>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Avatar sx={{ bgcolor: color + '20', color: color, width: 48, height: 48 }}>
            {icon}
          </Avatar>
          <Box sx={{ flexGrow: 1 }}>
            <Typography variant="h3" sx={{ color: color, fontWeight: 'bold' }}>
              {value}
            </Typography>
            <Typography variant="body2" color="textSecondary">
              {title}
            </Typography>
            <Typography variant="caption" color="textSecondary">
              {percentage.toFixed(1)}% of total devices
            </Typography>
          </Box>
        </Box>
      </CardContent>
    </Card>
  );
}

function DeviceStatus() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [selectedDevice, setSelectedDevice] = useState(null);

  const filteredDevices = mockDevices.filter(device => {
    const matchesSearch = device.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         device.id.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesStatus = statusFilter === 'all' || device.status === statusFilter;
    const matchesType = typeFilter === 'all' || device.type === typeFilter;
    
    return matchesSearch && matchesStatus && matchesType;
  });

  const operationalCount = mockDevices.filter(d => d.status === 'operational').length;
  const warningCount = mockDevices.filter(d => d.status === 'warning').length;
  const criticalCount = mockDevices.filter(d => d.status === 'critical').length;
  const maintenanceCount = mockDevices.filter(d => d.status === 'maintenance').length;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Box>
          <Typography variant="h4" gutterBottom>
            Device Status Monitor
          </Typography>
          <Typography variant="body1" color="textSecondary">
            Real-time monitoring of steel mill equipment health and performance
          </Typography>
        </Box>
        
        <Button
          variant="contained"
          startIcon={<RefreshIcon />}
          onClick={() => window.location.reload()}
        >
          Refresh
        </Button>
      </Box>

      {/* Summary Cards */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={6} lg={3}>
          <StatusSummaryCard
            title="Operational"
            value={operationalCount}
            total={mockDevices.length}
            color="#4caf50"
            icon={<CheckCircleIcon />}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={3}>
          <StatusSummaryCard
            title="Warning"
            value={warningCount}
            total={mockDevices.length}
            color="#ff9800"
            icon={<WarningIcon />}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={3}>
          <StatusSummaryCard
            title="Critical"
            value={criticalCount}
            total={mockDevices.length}
            color="#f44336"
            icon={<ErrorIcon />}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={3}>
          <StatusSummaryCard
            title="Maintenance"
            value={maintenanceCount}
            total={mockDevices.length}
            color="#9c27b0"
            icon={<PauseIcon />}
          />
        </Grid>
      </Grid>

      {/* Filters and Search */}
      <Card elevation={2} sx={{ mb: 3 }}>
        <CardContent>
          <Grid container spacing={2} alignItems="center">
            <Grid item xs={12} sm={6} md={4}>
              <TextField
                fullWidth
                size="small"
                placeholder="Search devices..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                InputProps={{
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon />
                    </InputAdornment>
                  ),
                }}
              />
            </Grid>
            
            <Grid item xs={12} sm={3} md={2}>
              <FormControl fullWidth size="small">
                <InputLabel>Status</InputLabel>
                <Select
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value)}
                  label="Status"
                >
                  <MenuItem value="all">All Status</MenuItem>
                  <MenuItem value="operational">Operational</MenuItem>
                  <MenuItem value="warning">Warning</MenuItem>
                  <MenuItem value="critical">Critical</MenuItem>
                  <MenuItem value="maintenance">Maintenance</MenuItem>
                </Select>
              </FormControl>
            </Grid>
            
            <Grid item xs={12} sm={3} md={2}>
              <FormControl fullWidth size="small">
                <InputLabel>Type</InputLabel>
                <Select
                  value={typeFilter}
                  onChange={(e) => setTypeFilter(e.target.value)}
                  label="Type"
                >
                  <MenuItem value="all">All Types</MenuItem>
                  <MenuItem value="Furnace">Furnace</MenuItem>
                  <MenuItem value="Rolling Mill">Rolling Mill</MenuItem>
                  <MenuItem value="Conveyor">Conveyor</MenuItem>
                  <MenuItem value="Caster">Caster</MenuItem>
                  <MenuItem value="Pump">Pump</MenuItem>
                  <MenuItem value="Compressor">Compressor</MenuItem>
                </Select>
              </FormControl>
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Grid container spacing={3}>
        {/* Device Cards Grid */}
        <Grid item xs={12} lg={8}>
          <Grid container spacing={2}>
            {filteredDevices.map((device) => (
              <Grid item xs={12} sm={6} md={4} key={device.id}>
                <DeviceCard device={device} />
              </Grid>
            ))}
          </Grid>
        </Grid>

        {/* Status Distribution and Zone Overview */}
        <Grid item xs={12} lg={4}>
          <Grid container spacing={3}>
            {/* Status Distribution */}
            <Grid item xs={12}>
              <Card elevation={2}>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Status Distribution
                  </Typography>
                  <ResponsiveContainer width="100%" height={200}>
                    <PieChart>
                      <Pie
                        data={mockStatusDistribution}
                        cx="50%"
                        cy="50%"
                        innerRadius={50}
                        outerRadius={80}
                        paddingAngle={5}
                        dataKey="value"
                      >
                        {mockStatusDistribution.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={entry.color} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                  <Box sx={{ mt: 1 }}>
                    {mockStatusDistribution.map((status, index) => (
                      <Chip
                        key={index}
                        label={`${status.name}: ${status.value}`}
                        size="small"
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

            {/* Zone Overview */}
            <Grid item xs={12}>
              <Card elevation={2}>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Zone Overview
                  </Typography>
                  <TableContainer>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>Zone</TableCell>
                          <TableCell align="center">Devices</TableCell>
                          <TableCell align="center">Status</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {mockZoneData.map((zone) => (
                          <TableRow key={zone.zone}>
                            <TableCell>
                              <Typography variant="body2" fontWeight="medium">
                                {zone.zone}
                              </Typography>
                            </TableCell>
                            <TableCell align="center">
                              <Typography variant="body2">
                                {zone.devices}
                              </Typography>
                            </TableCell>
                            <TableCell align="center">
                              <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'center' }}>
                                <Chip
                                  label={zone.operational}
                                  size="small"
                                  sx={{ 
                                    minWidth: 24,
                                    height: 20,
                                    fontSize: '0.75rem',
                                    backgroundColor: '#4caf5020',
                                    color: '#4caf50'
                                  }}
                                />
                                {zone.issues > 0 && (
                                  <Chip
                                    label={zone.issues}
                                    size="small"
                                    sx={{ 
                                      minWidth: 24,
                                      height: 20,
                                      fontSize: '0.75rem',
                                      backgroundColor: '#f4433620',
                                      color: '#f44336'
                                    }}
                                  />
                                )}
                              </Box>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </Grid>

        {/* Historical Telemetry */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Historical Telemetry Data
              </Typography>
              <ResponsiveContainer width="100%" height={300}>
                <AreaChart data={mockTelemetryHistory}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="time" />
                  <YAxis yAxisId="left" />
                  <YAxis yAxisId="right" orientation="right" />
                  <Tooltip />
                  <Area
                    yAxisId="left"
                    type="monotone"
                    dataKey="temperature"
                    stackId="1"
                    stroke="#ff6b6b"
                    fill="#ff6b6b"
                    fillOpacity={0.3}
                    name="Temperature (°C)"
                  />
                  <Area
                    yAxisId="right"
                    type="monotone"
                    dataKey="pressure"
                    stackId="2"
                    stroke="#4ecdc4"
                    fill="#4ecdc4"
                    fillOpacity={0.3}
                    name="Pressure (bar)"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}

export default DeviceStatus;