"""
Sinkii09 Engine Automation Core
Core utilities and shared functionality for automation scripts
"""
__version__ = "2.0.0"
__author__ = "Sinkii09 Engine Team"

from .config import Config
from .notion_client import NotionClient, NotionText
from .github_client import GitHubClient
from .logger import Logger, logger
from .workplan_parser import WorkPlanParser, WorkPlanItem, ItemType, WorkPlanTemplate

__all__ = ['Config', 'NotionClient', 'NotionText', 'GitHubClient', 'Logger', 'logger', 
          'WorkPlanParser', 'WorkPlanItem', 'ItemType', 'WorkPlanTemplate']