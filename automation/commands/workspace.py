#!/usr/bin/env python3
"""
Workspace management for Sinkii09 Engine
Handles creation and organization of Notion workspace structure
"""
from typing import Dict, List, Any, Optional

import sys
from pathlib import Path
sys.path.append(str(Path(__file__).parent.parent))

from core import Config, NotionClient, NotionText, logger

class WorkspaceManager:
    """Manages the Notion workspace structure"""
    
    def __init__(self, config: Config = None):
        self.config = config or Config()
        self.notion = NotionClient(self.config)
        self.dashboard_id = self.config.dashboard_id
    
    def get_workspace_structure(self) -> Dict[str, Dict[str, Any]]:
        """Define the desired workspace structure"""
        return {
            "🗺️ Project Roadmap": {
                "type": "page",
                "icon": "🗺️",
                "description": "High-level project timeline and milestones"
            },
            "📊 Sprint Planning Board": {
                "type": "database",
                "icon": "📊",
                "description": "Sprint tasks and backlog management",
                "properties": {
                    "Task": {"title": {}},
                    "Status": {
                        "select": {
                            "options": [
                                {"name": "Backlog", "color": "gray"},
                                {"name": "Sprint", "color": "blue"},
                                {"name": "In Progress", "color": "yellow"},
                                {"name": "Review", "color": "orange"},
                                {"name": "Done", "color": "green"}
                            ]
                        }
                    },
                    "Priority": {
                        "select": {
                            "options": [
                                {"name": "Low", "color": "gray"},
                                {"name": "Medium", "color": "yellow"},
                                {"name": "High", "color": "orange"},
                                {"name": "Critical", "color": "red"}
                            ]
                        }
                    },
                    "Sprint": {"select": {"options": []}},
                    "Assignee": {"people": {}},
                    "Story Points": {"number": {}},
                    "Due Date": {"date": {}}
                }
            },
            "🎯 Feature Requests": {
                "type": "database",
                "icon": "🎯",
                "description": "New feature proposals and enhancements",
                "properties": {
                    "Feature": {"title": {}},
                    "Status": {
                        "select": {
                            "options": [
                                {"name": "Proposed", "color": "gray"},
                                {"name": "Under Review", "color": "yellow"},
                                {"name": "Approved", "color": "green"},
                                {"name": "In Development", "color": "blue"},
                                {"name": "Completed", "color": "green"},
                                {"name": "Rejected", "color": "red"}
                            ]
                        }
                    },
                    "Priority": {
                        "select": {
                            "options": [
                                {"name": "Low", "color": "gray"},
                                {"name": "Medium", "color": "yellow"},
                                {"name": "High", "color": "orange"},
                                {"name": "Critical", "color": "red"}
                            ]
                        }
                    },
                    "Complexity": {
                        "select": {
                            "options": [
                                {"name": "Simple", "color": "green"},
                                {"name": "Medium", "color": "yellow"},
                                {"name": "Complex", "color": "red"}
                            ]
                        }
                    },
                    "Category": {"select": {"options": []}},
                    "Requester": {"people": {}},
                    "Created": {"created_time": {}},
                    "Updated": {"last_edited_time": {}}
                }
            },
            "🐛 Bug Reports": {
                "type": "database",
                "icon": "🐛",
                "description": "Bug tracking and resolution",
                "properties": {
                    "Bug": {"title": {}},
                    "Status": {
                        "select": {
                            "options": [
                                {"name": "Open", "color": "red"},
                                {"name": "In Progress", "color": "yellow"},
                                {"name": "Fixed", "color": "green"},
                                {"name": "Verified", "color": "green"},
                                {"name": "Closed", "color": "gray"},
                                {"name": "Won't Fix", "color": "gray"}
                            ]
                        }
                    },
                    "Severity": {
                        "select": {
                            "options": [
                                {"name": "Low", "color": "gray"},
                                {"name": "Medium", "color": "yellow"},
                                {"name": "High", "color": "orange"},
                                {"name": "Critical", "color": "red"}
                            ]
                        }
                    },
                    "Component": {"select": {"options": []}},
                    "Reporter": {"people": {}},
                    "Assignee": {"people": {}},
                    "Created": {"created_time": {}},
                    "Fixed": {"date": {}}
                }
            },
            "⚡ Performance Metrics": {
                "type": "database",
                "icon": "⚡",
                "description": "Performance tracking and optimization data",
                "properties": {
                    "Metric": {"title": {}},
                    "Value": {"number": {}},
                    "Unit": {"select": {"options": []}},
                    "Target": {"number": {}},
                    "Status": {
                        "select": {
                            "options": [
                                {"name": "Good", "color": "green"},
                                {"name": "Warning", "color": "yellow"},
                                {"name": "Critical", "color": "red"}
                            ]
                        }
                    },
                    "Category": {"select": {"options": []}},
                    "Date": {"date": {}},
                    "Notes": {"rich_text": {}}
                }
            },
            "🔧 DevOps & CI/CD": {
                "type": "page",
                "icon": "🔧", 
                "description": "Build pipelines, deployment processes, and automation"
            }
        }
    
    def find_existing_items(self) -> Dict[str, Optional[str]]:
        """Find existing workspace items"""
        logger.step("Scanning existing workspace items")
        
        # Get all child items of the dashboard
        children = self.notion.get_block_children(self.dashboard_id)
        existing = {}
        
        structure = self.get_workspace_structure()
        
        for title in structure.keys():
            existing[title] = None
            
            # Check if item already exists as child
            for child in children:
                if child.get('type') == 'child_page':
                    # For pages, we need to get the page to check title
                    child_page = self.notion.get_page(child['id'])
                    if child_page:
                        page_title = self._extract_page_title(child_page)
                        if title in page_title:
                            existing[title] = child['id']
                            logger.info(f"Found existing page: {title}")
                            break
                
                elif child.get('type') == 'child_database':
                    # For databases, title is in the child_database object
                    db_title = child.get('child_database', {}).get('title', '')
                    if title in db_title:
                        existing[title] = child['id']
                        logger.info(f"Found existing database: {title}")
                        break
        
        return existing
    
    def _extract_page_title(self, page: Dict[str, Any]) -> str:
        """Extract title from page object"""
        try:
            properties = page.get('properties', {})
            for prop_name, prop_data in properties.items():
                if prop_data.get('type') == 'title' and prop_data.get('title'):
                    return prop_data['title'][0]['text']['content']
        except (KeyError, IndexError):
            pass
        return ""
    
    def create_workspace_item(self, title: str, config: Dict[str, Any]) -> Optional[str]:
        """Create a workspace item (page or database)"""
        if config['type'] == 'page':
            return self.notion.create_page(
                parent_id=self.dashboard_id,
                title=title,
                icon=config.get('icon'),
                blocks=[
                    {
                        "object": "block",
                        "type": "paragraph",
                        "paragraph": {
                            "rich_text": [NotionText.create(config.get('description', 'No description'))]
                        }
                    }
                ]
            )
        
        elif config['type'] == 'database':
            return self.notion.create_database(
                parent_id=self.dashboard_id,
                title=title,
                icon=config.get('icon'),
                properties=config.get('properties')
            )
        
        return None
    
    def setup_workspace(self, force_recreate: bool = False) -> Dict[str, str]:
        """Set up the complete workspace structure"""
        logger.section("Workspace Setup")
        
        structure = self.get_workspace_structure()
        existing = self.find_existing_items()
        created_items = {}
        
        logger.step(f"Creating workspace items ({len(structure)} total)")
        
        for i, (title, config) in enumerate(structure.items()):
            logger.progress(i + 1, len(structure), title)
            
            if existing.get(title) and not force_recreate:
                logger.info(f"Skipping existing: {title}")
                created_items[title] = existing[title]
                continue
            
            item_id = self.create_workspace_item(title, config)
            if item_id:
                created_items[title] = item_id
                logger.success(f"Created {config['type']}: {title}")
            else:
                logger.error(f"Failed to create {config['type']}: {title}")
        
        # Update configuration with new IDs
        if created_items:
            self._update_workspace_config(created_items)
        
        logger.success(f"Workspace setup complete! Created {len([k for k, v in created_items.items() if k not in existing or not existing[k]])} new items")
        
        return created_items
    
    def _update_workspace_config(self, items: Dict[str, str]):
        """Update workspace configuration with new item IDs"""
        # Map titles to config keys
        title_to_key = {
            "🗺️ Project Roadmap": "PROJECT_ROADMAP_PAGE",
            "📊 Sprint Planning Board": "SPRINT_PLANNING_BOARD_DB", 
            "🎯 Feature Requests": "FEATURE_REQUESTS_DB",
            "🐛 Bug Reports": "BUG_REPORTS_DB",
            "⚡ Performance Metrics": "PERFORMANCE_METRICS_DB",
            "🔧 DevOps & CI/CD": "DEVOPS_&_CI/CD_DB"
        }
        
        new_config = {}
        for title, item_id in items.items():
            if title in title_to_key:
                new_config[title_to_key[title]] = item_id
        
        if new_config:
            self.config.update_workspace_config(new_config)
    
    def list_workspace_items(self) -> Dict[str, Any]:
        """List all workspace items and their status"""
        logger.section("Workspace Status")
        
        structure = self.get_workspace_structure()
        existing = self.find_existing_items()
        
        status = {
            "total": len(structure),
            "existing": 0,
            "missing": 0,
            "items": {}
        }
        
        logger.subsection("Workspace Items")
        
        for title, config in structure.items():
            exists = existing.get(title) is not None
            status["items"][title] = {
                "type": config["type"],
                "exists": exists,
                "id": existing.get(title),
                "description": config.get("description", "")
            }
            
            if exists:
                status["existing"] += 1
                logger.success(f"✅ {title} ({config['type']})")
            else:
                status["missing"] += 1
                logger.warning(f"❌ {title} ({config['type']}) - Missing")
        
        logger.info(f"Status: {status['existing']}/{status['total']} items exist")
        
        return status
    
    def clean_workspace(self, dry_run: bool = True):
        """Clean up workspace by removing duplicate or broken items"""
        logger.section("Workspace Cleanup")
        
        if dry_run:
            logger.info("DRY RUN - No changes will be made")
        
        children = self.notion.get_block_children(self.dashboard_id)
        logger.info(f"Found {len(children)} child items")
        
        # Analyze children
        pages = []
        databases = []
        content = []
        
        for child in children:
            block_type = child.get('type', 'unknown')
            if block_type == 'child_page':
                pages.append(child)
            elif block_type == 'child_database':
                databases.append(child)
            else:
                content.append(child)
        
        logger.subsection("Cleanup Summary")
        logger.info(f"Child pages: {len(pages)}")
        logger.info(f"Child databases: {len(databases)}")
        logger.info(f"Content blocks: {len(content)}")
        
        # Could add cleanup logic here if needed
        # For now, just report status
        
        return {
            "pages": len(pages),
            "databases": len(databases), 
            "content": len(content)
        }