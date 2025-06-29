#!/usr/bin/env python3
"""
Configuration management for Sinkii09 Engine automation
Handles environment variables, .env files, and workspace configuration
"""
import os
import sys
from pathlib import Path
from typing import Dict, Optional

class Config:
    """Central configuration manager for automation scripts"""
    
    def __init__(self, env_file: Optional[str] = None):
        self.project_root = self._find_project_root()
        self.env_file = env_file or self.project_root / '.env'
        self._config = {}
        self._load_configuration()
    
    def _find_project_root(self) -> Path:
        """Find the project root directory"""
        current = Path(__file__).parent
        while current.parent != current:
            if (current / '.env').exists() or (current / 'Assets').exists():
                return current
            current = current.parent
        return Path.cwd()
    
    def _load_env_file(self) -> bool:
        """Load environment variables from .env file"""
        if not self.env_file.exists():
            return False
        
        try:
            with open(self.env_file, 'r') as f:
                for line in f:
                    line = line.strip()
                    if line and not line.startswith('#') and '=' in line:
                        key, value = line.split('=', 1)
                        key = key.strip()
                        value = value.strip()
                        # Remove quotes if present
                        if value.startswith('"') and value.endswith('"'):
                            value = value[1:-1]
                        elif value.startswith("'") and value.endswith("'"):
                            value = value[1:-1]
                        os.environ[key] = value
                        self._config[key] = value
            return True
        except Exception as e:
            print(f"Warning: Failed to load .env file: {e}")
            return False
    
    # Legacy workspace config loading removed - now uses .env file only
    
    def _load_configuration(self):
        """Load all configuration sources"""
        # Load .env file first
        env_loaded = self._load_env_file()
        
        # Add environment variables (override .env if present)
        for key, value in os.environ.items():
            if key.startswith('NOTION_') or key.startswith('GITHUB_'):
                self._config[key] = value
        
        if env_loaded:
            print("✅ Configuration loaded from .env file")
        else:
            print("📝 Using environment variables only")
    
    def get(self, key: str, default: Optional[str] = None) -> Optional[str]:
        """Get configuration value"""
        return self._config.get(key, os.environ.get(key, default))
    
    def require(self, key: str) -> str:
        """Get required configuration value, exit if missing"""
        value = self.get(key)
        if not value:
            print(f"❌ Required configuration '{key}' not found!")
            print(f"   Please set it in .env file or export {key}='your-value'")
            sys.exit(1)
        return value
    
    def validate_tokens(self) -> Dict[str, bool]:
        """Validate all tokens and return status"""
        results = {}
        
        # Notion token
        notion_token = self.get('NOTION_TOKEN')
        if notion_token:
            valid = notion_token.startswith('ntn_') or notion_token.startswith('secret_')
            results['notion'] = valid
            if valid:
                print("✅ Notion token validated")
            else:
                print("⚠️  Notion token format may be invalid")
        else:
            results['notion'] = False
            print("❌ Notion token not found")
        
        # GitHub token  
        github_token = self.get('GITHUB_TOKEN')
        if github_token:
            valid = github_token.startswith('ghp_') or github_token.startswith('gho_')
            results['github'] = valid
            if valid:
                print("✅ GitHub token found")
            else:
                print("⚠️  GitHub token format may be invalid")
        else:
            results['github'] = False
            print("ℹ️  GitHub token not found (optional)")
        
        return results
    
    @property
    def notion_token(self) -> str:
        """Get Notion token (required)"""
        return self.require('NOTION_TOKEN')
    
    @property
    def github_token(self) -> Optional[str]:
        """Get GitHub token (optional)"""
        return self.get('GITHUB_TOKEN')
    
    @property
    def dashboard_id(self) -> str:
        """Get dashboard page ID"""
        return self.get('NOTION_DASHBOARD_ID', '22060dd5-2719-8059-8b73-ee12a0c80989')
    
    @property
    def workspace_pages(self) -> Dict[str, str]:
        """Get all workspace page IDs"""
        return {
            'roadmap': self.get('NOTION_PROJECT_ROADMAP_PAGE', '22060dd5-2719-81b8-b55b-e8e6daa0b507'),
            'devops': self.get('NOTION_DEVOPS_PAGE', '22060dd5-2719-818e-bdc8-c7b1271138c7'),
            'sprint_board': self.get('NOTION_SPRINT_PLANNING_BOARD_DB', '22060dd5-2719-81b5-bc69-f2d4a0a62d72'),
            'features': self.get('NOTION_FEATURE_REQUESTS_DB', '22060dd5-2719-8152-a0ad-ea979452f12c'),
            'bugs': self.get('NOTION_BUG_REPORTS_DB', '22060dd5-2719-81c3-bd10-c57c82cad434'),
            'metrics': self.get('NOTION_PERFORMANCE_METRICS_DB', '22060dd5-2719-816f-943b-d288041c969a'),
            'roadmap_db': self.get('NOTION_ROADMAP_DB', '22060dd5271981a48d00ff14829c6a39')
        }
    
    def update_workspace_config(self, new_pages: Dict[str, str]):
        """Update workspace configuration with new page IDs - now updates internal config only"""
        # Update internal config and environment variables
        for key, value in new_pages.items():
            env_key = f"NOTION_{key.upper()}"
            if key.endswith('_db'):
                env_key = f"NOTION_{key.upper()}"
            else:
                env_key = f"NOTION_{key.upper()}_DB" if 'board' in key or 'requests' in key or 'reports' in key or 'metrics' in key else f"NOTION_{key.upper()}_PAGE"
            
            self._config[env_key] = value
            os.environ[env_key] = value
        
        print(f"✅ Updated workspace configuration in memory")
    
    def __str__(self) -> str:
        """String representation for debugging"""
        return f"Config(root={self.project_root}, tokens={'✅' if self.get('NOTION_TOKEN') else '❌'})"