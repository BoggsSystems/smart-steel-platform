import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Divider,
  Box,
} from '@mui/material';
import {
  Dashboard as DashboardIcon,
  Analytics,
  Devices,
  Warning,
  Memory,
  TrendingUp,
} from '@mui/icons-material';

const drawerWidth = 240;

const menuItems = [
  { text: 'Dashboard', icon: <DashboardIcon />, path: '/' },
  { text: 'Model Monitoring', icon: <Analytics />, path: '/monitoring' },
  { text: 'Device Status', icon: <Devices />, path: '/devices' },
  { text: 'Alerts', icon: <Warning />, path: '/alerts' },
];

function Sidebar({ open }) {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Drawer
      variant="persistent"
      anchor="left"
      open={open}
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: drawerWidth,
          boxSizing: 'border-box',
          mt: 8,
        },
      }}
    >
      <Box sx={{ overflow: 'auto' }}>
        <List>
          {menuItems.map((item) => (
            <ListItem key={item.text} disablePadding>
              <ListItemButton
                selected={location.pathname === item.path}
                onClick={() => navigate(item.path)}
                sx={{
                  '&.Mui-selected': {
                    backgroundColor: 'rgba(25, 118, 210, 0.08)',
                    borderRight: '3px solid #1976d2',
                  },
                }}
              >
                <ListItemIcon
                  sx={{
                    color: location.pathname === item.path ? '#1976d2' : 'inherit',
                  }}
                >
                  {item.icon}
                </ListItemIcon>
                <ListItemText 
                  primary={item.text}
                  sx={{
                    '& .MuiListItemText-primary': {
                      fontWeight: location.pathname === item.path ? 600 : 400,
                    },
                  }}
                />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
        
        <Divider />
        
        <List>
          <ListItem>
            <ListItemIcon>
              <Memory />
            </ListItemIcon>
            <ListItemText primary="System Status" secondary="All systems operational" />
          </ListItem>
          
          <ListItem>
            <ListItemIcon>
              <TrendingUp />
            </ListItemIcon>
            <ListItemText primary="Performance" secondary="98.5% uptime" />
          </ListItem>
        </List>
      </Box>
    </Drawer>
  );
}

export default Sidebar;