import React from 'react';
import {
  AppBar,
  Toolbar,
  Typography,
  IconButton,
  Box,
  Chip,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Notifications,
  Settings,
} from '@mui/icons-material';

function Header({ onMenuClick }) {
  return (
    <AppBar
      position="fixed"
      sx={{
        zIndex: (theme) => theme.zIndex.drawer + 1,
        backgroundColor: '#1565c0',
      }}
    >
      <Toolbar>
        <IconButton
          color="inherit"
          aria-label="open drawer"
          onClick={onMenuClick}
          edge="start"
          sx={{ mr: 2 }}
        >
          <MenuIcon />
        </IconButton>
        
        <Typography variant="h6" noWrap component="div" sx={{ flexGrow: 1 }}>
          Steel Mill AI Platform
        </Typography>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Chip
            label="Production"
            color="success"
            variant="outlined"
            size="small"
            sx={{ color: 'white', borderColor: 'rgba(255,255,255,0.5)' }}
          />
          
          <IconButton color="inherit">
            <Notifications />
          </IconButton>
          
          <IconButton color="inherit">
            <Settings />
          </IconButton>
        </Box>
      </Toolbar>
    </AppBar>
  );
}

export default Header;