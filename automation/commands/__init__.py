"""
Sinkii09 Engine Automation Commands
Main automation commands for managing the project
"""
from .dashboard import DashboardManager
from .workspace import WorkspaceManager
from .github_sync import GitHubSyncManager

__all__ = ['DashboardManager', 'WorkspaceManager', 'GitHubSyncManager']