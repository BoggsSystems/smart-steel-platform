import React, { useState, useEffect } from 'react';
import {
  Box,
  Grid,
  Card,
  CardContent,
  Typography,
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
  TextField,
  InputAdornment,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Alert,
  Badge,
  Avatar,
  Menu,
  Tabs,
  Tab,
  Divider,
  ListItemIcon,
  ListItemText,
  MenuItem as MenuItemComponent,
} from '@mui/material';
import {
  Search as SearchIcon,
  FilterList as FilterListIcon,
  Notifications as NotificationsIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  Info as InfoIcon,
  CheckCircle as CheckCircleIcon,
  MoreVert as MoreVertIcon,
  Delete as DeleteIcon,
  Archive as ArchiveIcon,
  Assignment as AssignmentIcon,
  Schedule as ScheduleIcon,
  Visibility as VisibilityIcon,
  NotificationsOff as NotificationsOffIcon,
} from '@mui/icons-material';
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
  AreaChart,
  Area,
} from 'recharts';

// Mock alerts data
const mockAlerts = [
  {
    id: 'ALT-001',
    title: 'High Vibration Detected',
    description: 'Rolling Mill 2 showing abnormal vibration levels (0.12 vs 0.04 normal)',
    severity: 'critical',
    category: 'Equipment',
    device: 'Rolling Mill 2',
    location: 'Zone B',
    timestamp: '2024-01-20T10:25:00Z',
    status: 'active',
    assignedTo: 'John Smith',
    priority: 'high',
    source: 'Vibration Sensor',
    acknowledgedBy: null,
    acknowledgedAt: null,
    resolvedBy: null,
    resolvedAt: null,
  },
  {
    id: 'ALT-002',
    title: 'Temperature Anomaly',
    description: 'Furnace 1 temperature exceeded normal range (1720°C vs 1650°C max)',
    severity: 'warning',
    category: 'Process',
    device: 'Electric Arc Furnace 1',
    location: 'Zone A',
    timestamp: '2024-01-20T09:45:00Z',
    status: 'acknowledged',
    assignedTo: 'Sarah Johnson',
    priority: 'medium',
    source: 'Temperature Sensor',
    acknowledgedBy: 'Sarah Johnson',
    acknowledgedAt: '2024-01-20T09:50:00Z',
    resolvedBy: null,
    resolvedAt: null,
  },
  {
    id: 'ALT-003',
    title: 'Scheduled Maintenance Due',
    description: 'Conveyor System 3 scheduled for maintenance within 24 hours',
    severity: 'info',
    category: 'Maintenance',
    device: 'Conveyor System 3',
    location: 'Zone C',
    timestamp: '2024-01-20T08:30:00Z',
    status: 'active',
    assignedTo: 'Mike Davis',
    priority: 'low',
    source: 'Maintenance Schedule',
    acknowledgedBy: null,
    acknowledgedAt: null,
    resolvedBy: null,
    resolvedAt: null,
  },
  {
    id: 'ALT-004',
    title: 'Power Consumption Spike',
    description: 'Energy consumption 25% above normal for past 2 hours',
    severity: 'warning',
    category: 'Energy',
    device: 'Plant Wide',
    location: 'All Zones',
    timestamp: '2024-01-20T08:00:00Z',
    status: 'resolved',
    assignedTo: 'Lisa Chen',
    priority: 'medium',
    source: 'Energy Monitor',
    acknowledgedBy: 'Lisa Chen',
    acknowledgedAt: '2024-01-20T08:15:00Z',
    resolvedBy: 'Lisa Chen',
    resolvedAt: '2024-01-20T09:30:00Z',
  },
  {
    id: 'ALT-005',
    title: 'Motor Temperature Critical',
    description: 'Conveyor motor temperature reached critical threshold (95°C)',
    severity: 'critical',
    category: 'Equipment',
    device: 'Conveyor System 3',
    location: 'Zone C',
    timestamp: '2024-01-20T07:15:00Z',
    status: 'acknowledged',
    assignedTo: 'Tom Wilson',
    priority: 'high',
    source: 'Motor Sensor',
    acknowledgedBy: 'Tom Wilson',
    acknowledgedAt: '2024-01-20T07:20:00Z',
    resolvedBy: null,
    resolvedAt: null,
  },
];

// Mock alert trend data
const mockAlertTrend = [
  { date: '2024-01-14', critical: 2, warning: 5, info: 3 },
  { date: '2024-01-15', critical: 1, warning: 7, info: 4 },
  { date: '2024-01-16', critical: 3, warning: 4, info: 2 },
  { date: '2024-01-17', critical: 0, warning: 6, info: 5 },
  { date: '2024-01-18', critical: 2, warning: 3, info: 6 },
  { date: '2024-01-19', critical: 1, warning: 8, info: 3 },
  { date: '2024-01-20', critical: 2, warning: 2, info: 1 },
];

// Mock alert distribution data
const mockSeverityDistribution = [
  { name: 'Critical', value: 2, color: '#f44336' },
  { name: 'Warning', value: 2, color: '#ff9800' },
  { name: 'Info', value: 1, color: '#2196f3' },
];

const mockCategoryDistribution = [
  { name: 'Equipment', value: 3, color: '#e91e63' },
  { name: 'Process', value: 1, color: '#9c27b0' },
  { name: 'Energy', value: 1, color: '#3f51b5' },
  { name: 'Maintenance', value: 1, color: '#00bcd4' },
];

function AlertCard({ alert, onAction }) {
  const [anchorEl, setAnchorEl] = useState(null);

  const getSeverityIcon = (severity) => {
    switch (severity) {
      case 'critical':
        return <ErrorIcon sx={{ color: '#f44336' }} />;
      case 'warning':
        return <WarningIcon sx={{ color: '#ff9800' }} />;
      case 'info':
        return <InfoIcon sx={{ color: '#2196f3' }} />;
      default:
        return <InfoIcon sx={{ color: '#757575' }} />;
    }
  };

  const getSeverityColor = (severity) => {
    switch (severity) {
      case 'critical': return 'error';
      case 'warning': return 'warning';
      case 'info': return 'info';
      default: return 'default';
    }
  };

  const getStatusColor = (status) => {
    switch (status) {
      case 'active': return 'error';
      case 'acknowledged': return 'warning';
      case 'resolved': return 'success';
      default: return 'default';
    }
  };

  const formatTimestamp = (timestamp) => {
    return new Date(timestamp).toLocaleString();
  };

  const handleMenuClick = (event) => {
    setAnchorEl(event.currentTarget);
  };

  const handleMenuClose = () => {
    setAnchorEl(null);
  };

  const handleAction = (action) => {
    onAction(alert.id, action);
    handleMenuClose();
  };

  return (
    <Card 
      elevation={2} 
      sx={{ 
        mb: 2,
        borderLeft: `4px solid ${alert.severity === 'critical' ? '#f44336' : 
                                  alert.severity === 'warning' ? '#ff9800' : '#2196f3'}`,
        opacity: alert.status === 'resolved' ? 0.7 : 1
      }}
    >
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 2, flexGrow: 1 }}>
            {getSeverityIcon(alert.severity)}
            
            <Box sx={{ flexGrow: 1 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                <Typography variant="h6" component="div">
                  {alert.title}
                </Typography>
                <Chip
                  label={alert.severity.toUpperCase()}
                  color={getSeverityColor(alert.severity)}
                  size="small"
                />
                <Chip
                  label={alert.status.toUpperCase()}
                  color={getStatusColor(alert.status)}
                  variant="outlined"
                  size="small"
                />
              </Box>
              
              <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
                {alert.description}
              </Typography>
              
              <Grid container spacing={2}>
                <Grid item xs={12} sm={6} md={3}>
                  <Typography variant="caption" color="textSecondary" display="block">
                    Device
                  </Typography>
                  <Typography variant="body2">{alert.device}</Typography>
                </Grid>
                
                <Grid item xs={12} sm={6} md={3}>
                  <Typography variant="caption" color="textSecondary" display="block">
                    Location
                  </Typography>
                  <Typography variant="body2">{alert.location}</Typography>
                </Grid>
                
                <Grid item xs={12} sm={6} md={3}>
                  <Typography variant="caption" color="textSecondary" display="block">
                    Assigned To
                  </Typography>
                  <Typography variant="body2">{alert.assignedTo}</Typography>
                </Grid>
                
                <Grid item xs={12} sm={6} md={3}>
                  <Typography variant="caption" color="textSecondary" display="block">
                    Timestamp
                  </Typography>
                  <Typography variant="body2">{formatTimestamp(alert.timestamp)}</Typography>
                </Grid>
              </Grid>
              
              {alert.acknowledgedBy && (
                <Box sx={{ mt: 1, pt: 1, borderTop: '1px solid #e0e0e0' }}>
                  <Typography variant="caption" color="textSecondary">
                    Acknowledged by {alert.acknowledgedBy} at {formatTimestamp(alert.acknowledgedAt)}
                  </Typography>
                </Box>
              )}
              
              {alert.resolvedBy && (
                <Box sx={{ mt: 1, pt: 1, borderTop: '1px solid #e0e0e0' }}>
                  <Typography variant="caption" color="success.main">
                    Resolved by {alert.resolvedBy} at {formatTimestamp(alert.resolvedAt)}
                  </Typography>
                </Box>
              )}
            </Box>
          </Box>
          
          <IconButton onClick={handleMenuClick} size="small">
            <MoreVertIcon />
          </IconButton>
          
          <Menu
            anchorEl={anchorEl}
            open={Boolean(anchorEl)}
            onClose={handleMenuClose}
          >
            {alert.status === 'active' && (
              <MenuItemComponent onClick={() => handleAction('acknowledge')}>
                <ListItemIcon>
                  <VisibilityIcon fontSize="small" />
                </ListItemIcon>
                <ListItemText>Acknowledge</ListItemText>
              </MenuItemComponent>
            )}
            
            {alert.status === 'acknowledged' && (
              <MenuItemComponent onClick={() => handleAction('resolve')}>
                <ListItemIcon>
                  <CheckCircleIcon fontSize="small" />
                </ListItemIcon>
                <ListItemText>Mark Resolved</ListItemText>
              </MenuItemComponent>
            )}
            
            <MenuItemComponent onClick={() => handleAction('assign')}>
              <ListItemIcon>
                <AssignmentIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Reassign</ListItemText>
            </MenuItemComponent>
            
            <MenuItemComponent onClick={() => handleAction('snooze')}>
              <ListItemIcon>
                <NotificationsOffIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Snooze</ListItemText>
            </MenuItemComponent>
            
            <Divider />
            
            <MenuItemComponent onClick={() => handleAction('archive')}>
              <ListItemIcon>
                <ArchiveIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Archive</ListItemText>
            </MenuItemComponent>
            
            <MenuItemComponent onClick={() => handleAction('delete')}>
              <ListItemIcon>
                <DeleteIcon fontSize="small" color="error" />
              </ListItemIcon>
              <ListItemText sx={{ color: 'error.main' }}>Delete</ListItemText>
            </MenuItemComponent>
          </Menu>
        </Box>
      </CardContent>
    </Card>
  );
}

function StatCard({ title, value, change, color, icon }) {
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
            {change && (
              <Typography variant="caption" color={change > 0 ? 'error.main' : 'success.main'}>
                {change > 0 ? '+' : ''}{change}% from yesterday
              </Typography>
            )}
          </Box>
        </Box>
      </CardContent>
    </Card>
  );
}

function Alerts() {
  const [tabValue, setTabValue] = useState(0);
  const [searchTerm, setSearchTerm] = useState('');
  const [severityFilter, setSeverityFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');

  const filteredAlerts = mockAlerts.filter(alert => {
    const matchesSearch = alert.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         alert.device.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         alert.description.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesSeverity = severityFilter === 'all' || alert.severity === severityFilter;
    const matchesStatus = statusFilter === 'all' || alert.status === statusFilter;
    const matchesCategory = categoryFilter === 'all' || alert.category === categoryFilter;
    
    // Tab filtering
    if (tabValue === 1 && alert.status === 'resolved') return false; // Active alerts only
    if (tabValue === 2 && alert.status !== 'resolved') return false; // Resolved alerts only
    
    return matchesSearch && matchesSeverity && matchesStatus && matchesCategory;
  });

  const activeAlerts = mockAlerts.filter(a => a.status !== 'resolved');
  const criticalAlerts = mockAlerts.filter(a => a.severity === 'critical' && a.status !== 'resolved');
  const warningAlerts = mockAlerts.filter(a => a.severity === 'warning' && a.status !== 'resolved');
  const resolvedToday = mockAlerts.filter(a => a.status === 'resolved');

  const handleAlertAction = (alertId, action) => {
    console.log(`Action ${action} performed on alert ${alertId}`);
    // In real implementation, this would make API calls to update alert status
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Alerts Management
      </Typography>
      
      <Typography variant="body1" color="textSecondary" gutterBottom sx={{ mb: 3 }}>
        Monitor, manage, and respond to system alerts and notifications
      </Typography>

      {/* Summary Stats */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={6} lg={3}>
          <StatCard
            title="Active Alerts"
            value={activeAlerts.length}
            change={2}
            color="#f44336"
            icon={<NotificationsIcon />}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={3}>
          <StatCard
            title="Critical"
            value={criticalAlerts.length}
            change={1}
            color="#d32f2f"
            icon={<ErrorIcon />}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={3}>
          <StatCard
            title="Warnings"
            value={warningAlerts.length}
            change={-1}
            color="#ff9800"
            icon={<WarningIcon />}
          />
        </Grid>
        
        <Grid item xs={12} sm={6} lg={3}>
          <StatCard
            title="Resolved Today"
            value={resolvedToday.length}
            change={0}
            color="#4caf50"
            icon={<CheckCircleIcon />}
          />
        </Grid>
      </Grid>

      <Grid container spacing={3}>
        {/* Alert Trends */}
        <Grid item xs={12} lg={8}>
          <Card elevation={2} sx={{ mb: 3 }}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Alert Trends (Last 7 Days)
              </Typography>
              <ResponsiveContainer width="100%" height={250}>
                <AreaChart data={mockAlertTrend}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" />
                  <YAxis />
                  <Tooltip />
                  <Area
                    type="monotone"
                    dataKey="critical"
                    stackId="1"
                    stroke="#f44336"
                    fill="#f44336"
                    fillOpacity={0.8}
                    name="Critical"
                  />
                  <Area
                    type="monotone"
                    dataKey="warning"
                    stackId="1"
                    stroke="#ff9800"
                    fill="#ff9800"
                    fillOpacity={0.8}
                    name="Warning"
                  />
                  <Area
                    type="monotone"
                    dataKey="info"
                    stackId="1"
                    stroke="#2196f3"
                    fill="#2196f3"
                    fillOpacity={0.8}
                    name="Info"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Alert Distribution */}
        <Grid item xs={12} lg={4}>
          <Grid container spacing={3}>
            <Grid item xs={12}>
              <Card elevation={2}>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    By Severity
                  </Typography>
                  <ResponsiveContainer width="100%" height={150}>
                    <PieChart>
                      <Pie
                        data={mockSeverityDistribution}
                        cx="50%"
                        cy="50%"
                        innerRadius={30}
                        outerRadius={60}
                        paddingAngle={5}
                        dataKey="value"
                      >
                        {mockSeverityDistribution.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={entry.color} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            
            <Grid item xs={12}>
              <Card elevation={2}>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    By Category
                  </Typography>
                  <ResponsiveContainer width="100%" height={150}>
                    <PieChart>
                      <Pie
                        data={mockCategoryDistribution}
                        cx="50%"
                        cy="50%"
                        innerRadius={30}
                        outerRadius={60}
                        paddingAngle={5}
                        dataKey="value"
                      >
                        {mockCategoryDistribution.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={entry.color} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </Grid>

        {/* Alert Management Interface */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              {/* Tabs */}
              <Tabs
                value={tabValue}
                onChange={(event, newValue) => setTabValue(newValue)}
                sx={{ mb: 3 }}
              >
                <Tab 
                  label={
                    <Badge badgeContent={mockAlerts.length} color="primary">
                      All Alerts
                    </Badge>
                  } 
                />
                <Tab 
                  label={
                    <Badge badgeContent={activeAlerts.length} color="error">
                      Active
                    </Badge>
                  } 
                />
                <Tab 
                  label={
                    <Badge badgeContent={resolvedToday.length} color="success">
                      Resolved
                    </Badge>
                  } 
                />
              </Tabs>

              {/* Filters */}
              <Grid container spacing={2} alignItems="center" sx={{ mb: 3 }}>
                <Grid item xs={12} sm={6} md={3}>
                  <TextField
                    fullWidth
                    size="small"
                    placeholder="Search alerts..."
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
                
                <Grid item xs={12} sm={6} md={2}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Severity</InputLabel>
                    <Select
                      value={severityFilter}
                      onChange={(e) => setSeverityFilter(e.target.value)}
                      label="Severity"
                    >
                      <MenuItem value="all">All Severities</MenuItem>
                      <MenuItem value="critical">Critical</MenuItem>
                      <MenuItem value="warning">Warning</MenuItem>
                      <MenuItem value="info">Info</MenuItem>
                    </Select>
                  </FormControl>
                </Grid>
                
                <Grid item xs={12} sm={6} md={2}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Status</InputLabel>
                    <Select
                      value={statusFilter}
                      onChange={(e) => setStatusFilter(e.target.value)}
                      label="Status"
                    >
                      <MenuItem value="all">All Status</MenuItem>
                      <MenuItem value="active">Active</MenuItem>
                      <MenuItem value="acknowledged">Acknowledged</MenuItem>
                      <MenuItem value="resolved">Resolved</MenuItem>
                    </Select>
                  </FormControl>
                </Grid>
                
                <Grid item xs={12} sm={6} md={2}>
                  <FormControl fullWidth size="small">
                    <InputLabel>Category</InputLabel>
                    <Select
                      value={categoryFilter}
                      onChange={(e) => setCategoryFilter(e.target.value)}
                      label="Category"
                    >
                      <MenuItem value="all">All Categories</MenuItem>
                      <MenuItem value="Equipment">Equipment</MenuItem>
                      <MenuItem value="Process">Process</MenuItem>
                      <MenuItem value="Energy">Energy</MenuItem>
                      <MenuItem value="Maintenance">Maintenance</MenuItem>
                    </Select>
                  </FormControl>
                </Grid>
              </Grid>

              {/* Alert List */}
              <Box>
                {filteredAlerts.length === 0 ? (
                  <Box sx={{ textAlign: 'center', py: 4 }}>
                    <NotificationsIcon sx={{ fontSize: 64, color: 'text.secondary', mb: 2 }} />
                    <Typography variant="h6" color="textSecondary">
                      No alerts match your current filters
                    </Typography>
                  </Box>
                ) : (
                  filteredAlerts.map((alert) => (
                    <AlertCard
                      key={alert.id}
                      alert={alert}
                      onAction={handleAlertAction}
                    />
                  ))
                )}
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}

export default Alerts;