#!/usr/bin/env python3
"""
Dashboard management for Sinkii09 Engine
Unified dashboard update with data preservation
"""
from typing import Dict, List, Any
from datetime import datetime

import sys
from pathlib import Path
sys.path.append(str(Path(__file__).parent.parent))

from core import Config, NotionClient, GitHubClient, NotionText, logger

class DashboardManager:
    """Manages the main project dashboard"""
    
    def __init__(self, config: Config = None):
        self.config = config or Config()
        self.notion = NotionClient(self.config)
        self.github = GitHubClient(self.config)
        self.dashboard_id = self.config.dashboard_id
    
    def create_progress_bar(self, percentage: int, label: str = "") -> str:
        """Create a visual progress bar using emojis"""
        filled = int(percentage / 10)
        empty = 10 - filled
        bar = "🟩" * filled + "⬜" * empty
        return f"{label} {bar} {percentage}%"
    
    def create_header_section(self, github_stats: Dict[str, Any]) -> List[Dict[str, Any]]:
        """Create the header section with project info"""
        return [
            {
                "object": "block",
                "type": "heading_1", 
                "heading_1": {
                    "rich_text": [NotionText.create("🎮 Sinkii09 Engine - Project Dashboard", color="blue")]
                }
            },
            {
                "object": "block",
                "type": "paragraph",
                "paragraph": {
                    "rich_text": [
                        NotionText.create("A modular, service-oriented game engine framework for Unity | "),
                        NotionText.create(f"⭐ {github_stats['stars']} stars | 🔀 {github_stats['forks']} forks | 👀 {github_stats['watchers']} watchers")
                    ]
                }
            },
            {
                "object": "block",
                "type": "paragraph",
                "paragraph": {
                    "rich_text": [
                        NotionText.create(f"📅 Last commit: {github_stats['last_commit']} | ", color="gray"),
                        NotionText.create(f"🐛 Open issues: {github_stats['open_issues']} | ", color="gray"),
                        NotionText.create(f"✅ Closed: {github_stats['closed_issues']}", color="gray")
                    ]
                }
            },
            {
                "object": "block",
                "type": "divider",
                "divider": {}
            }
        ]
    
    def create_progress_section(self) -> List[Dict[str, Any]]:
        """Create the progress overview section"""
        overall_progress = 12
        phase1_progress = 15
        service_progress = 40
        resource_progress = 20
        
        return [
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("📊 Development Progress")]
                }
            },
            {
                "object": "block",
                "type": "callout",
                "callout": {
                    "rich_text": [NotionText.create(self.create_progress_bar(overall_progress, "Overall:"))],
                    "icon": {"emoji": "🎯"},
                    "color": "green_background"
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create(self.create_progress_bar(phase1_progress, "Phase 1:"))]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create(self.create_progress_bar(service_progress, "Services:"))]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create(self.create_progress_bar(resource_progress, "Resources:"))]
                }
            }
        ]
    
    def create_navigation_section(self) -> List[Dict[str, Any]]:
        """Create the navigation section with links to all pages"""
        workspace_pages = self.config.workspace_pages
        
        return [
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("🧭 Quick Navigation")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("🗺️ "),
                        NotionText.create("Project Roadmap", bold=True, 
                                       link=f"https://notion.so/{workspace_pages['roadmap'].replace('-', '')}"),
                        NotionText.create(" - High-level milestones and timeline")
                    ]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("📅 "),
                        NotionText.create("Sprint Board", bold=True,
                                       link=f"https://notion.so/{workspace_pages['sprint_board'].replace('-', '')}"),
                        NotionText.create(" - Current sprint tasks and backlog")
                    ]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("⚙️ "),
                        NotionText.create("DevOps & CI/CD", bold=True,
                                       link=f"https://notion.so/{workspace_pages['devops'].replace('-', '')}"),
                        NotionText.create(" - Build pipelines and automation")
                    ]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("🎯 "),
                        NotionText.create("Features", bold=True,
                                       link=f"https://notion.so/{workspace_pages['features'].replace('-', '')}"),
                        NotionText.create(" | "),
                        NotionText.create("🐛 "),
                        NotionText.create("Bugs", bold=True,
                                       link=f"https://notion.so/{workspace_pages['bugs'].replace('-', '')}"),
                        NotionText.create(" | "),
                        NotionText.create("📊 "),
                        NotionText.create("Metrics", bold=True,
                                       link=f"https://notion.so/{workspace_pages['metrics'].replace('-', '')}")
                    ]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("📋 "),
                        NotionText.create("GitHub Issues", bold=True,
                                       link=f"https://notion.so/{workspace_pages['roadmap_db'].replace('-', '')}"),
                        NotionText.create(" - Development issues and task tracking")
                    ]
                }
            }
        ]
    
    def create_sprint_section(self) -> List[Dict[str, Any]]:
        """Create current sprint status section"""
        return [
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("🏃 Current Sprint")]
                }
            },
            {
                "object": "block",
                "type": "callout",
                "callout": {
                    "rich_text": [
                        NotionText.create("Enhanced Service Architecture", bold=True),
                        NotionText.create(" - Implementing DI, state management, and error handling")
                    ],
                    "icon": {"emoji": "⚡"},
                    "color": "yellow_background"
                }
            },
            {
                "object": "block",
                "type": "to_do",
                "to_do": {
                    "rich_text": [NotionText.create("Enhanced IEngineService Interface")],
                    "checked": False
                }
            },
            {
                "object": "block",
                "type": "to_do",
                "to_do": {
                    "rich_text": [NotionText.create("Service Container with Dependency Injection")],
                    "checked": False
                }
            },
            {
                "object": "block",
                "type": "to_do",
                "to_do": {
                    "rich_text": [NotionText.create("Topological Service Initialization")],
                    "checked": False
                }
            },
            {
                "object": "block",
                "type": "to_do",
                "to_do": {
                    "rich_text": [NotionText.create("Service State Management")],
                    "checked": False
                }
            }
        ]
    
    def create_system_status_section(self) -> List[Dict[str, Any]]:
        """Create system status section"""
        return [
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("🎯 System Status")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create("✅ Engine Core - Implemented")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create("✅ Service Locator - Implemented")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create("✅ Command System - Implemented")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create("⚠️ Resource Service - Basic implementation")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [NotionText.create("❌ Actor System - Not started")]
                }
            }
        ]
    
    def create_commands_section(self) -> List[Dict[str, Any]]:
        """Create quick commands section"""
        return [
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("⚡ Quick Commands")]
                }
            },
            {
                "object": "block",
                "type": "code",
                "code": {
                    "rich_text": [NotionText.create(
                        "# Full project sync\\n"
                        "./automation/engine sync\\n\\n"
                        "# Update dashboard only\\n"
                        "./automation/engine dashboard\\n\\n"
                        "# Sync GitHub issues\\n"
                        "./automation/engine github\\n\\n"
                        "# Setup workspace\\n"
                        "./automation/engine workspace setup"
                    )],
                    "language": "bash"
                }
            }
        ]
    
    def create_resources_section(self) -> List[Dict[str, Any]]:
        """Create resources and links section"""
        return [
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("📚 Resources")]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("🐙 "),
                        NotionText.create("GitHub Repository", bold=True,
                                       link="https://github.com/Sinkii09/Engine")
                    ]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("📖 "),
                        NotionText.create("Unity Documentation", bold=True,
                                       link="https://docs.unity3d.com/Manual/")
                    ]
                }
            },
            {
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [
                        NotionText.create("📘 "),
                        NotionText.create("Project Wiki", bold=True,
                                       link="https://github.com/Sinkii09/Engine/wiki")
                    ]
                }
            }
        ]
    
    def create_footer_section(self) -> List[Dict[str, Any]]:
        """Create footer with update info"""
        return [
            {
                "object": "block",
                "type": "divider",
                "divider": {}
            },
            {
                "object": "block",
                "type": "paragraph",
                "paragraph": {
                    "rich_text": [
                        NotionText.create(f"🤖 Auto-updated: {datetime.now().strftime('%Y-%m-%d %H:%M UTC')} | ", 
                                       italic=True, color="gray"),
                        NotionText.create("Powered by Sinkii09 Engine Automation v2.0", italic=True, color="gray")
                    ]
                }
            },
            {
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [NotionText.create("🗃️ Project Databases")]
                }
            },
            {
                "object": "block",
                "type": "paragraph",
                "paragraph": {
                    "rich_text": [
                        NotionText.create("Child databases and pages are automatically managed and appear below.")
                    ]
                }
            }
        ]
    
    def update_dashboard(self, preserve_databases: bool = True) -> bool:
        """Update the dashboard with latest information"""
        logger.section("Dashboard Update")
        
        # Clear content while preserving databases
        preserved_items = self.notion.clear_content_blocks(self.dashboard_id, preserve_databases)
        
        # Get GitHub stats
        logger.step("Fetching GitHub statistics")
        github_stats = self.github.get_repository_stats()
        
        # Create all sections
        logger.step("Building dashboard content")
        all_blocks = []
        
        # Add all sections
        all_blocks.extend(self.create_header_section(github_stats))
        all_blocks.extend(self.create_progress_section())
        all_blocks.extend(self.create_navigation_section())
        all_blocks.extend(self.create_sprint_section())
        all_blocks.extend(self.create_system_status_section())
        all_blocks.extend(self.create_commands_section())
        all_blocks.extend(self.create_resources_section())
        all_blocks.extend(self.create_footer_section())
        
        # Add content in smaller batches for reliability
        logger.step("Uploading dashboard content")
        batch_size = 10
        success_count = 0
        total_batches = (len(all_blocks) + batch_size - 1) // batch_size
        
        for i in range(0, len(all_blocks), batch_size):
            batch = all_blocks[i:i+batch_size]
            batch_num = (i // batch_size) + 1
            
            logger.progress(batch_num, total_batches, f"Batch {batch_num}/{total_batches}")
            
            if self.notion.append_blocks(self.dashboard_id, batch):
                success_count += 1
            else:
                logger.error(f"Failed to add batch {batch_num}")
        
        # Results
        total_blocks = len(all_blocks)
        if success_count == total_batches:
            logger.success(f"Dashboard updated successfully! ({total_blocks} blocks added)")
            logger.success(f"Preserved {len(preserved_items)} databases/pages")
            logger.info(f"View dashboard: https://notion.so/{self.dashboard_id.replace('-', '')}")
            return True
        else:
            logger.error(f"Dashboard update incomplete ({success_count}/{total_batches} batches succeeded)")
            return False
    
    def check_structure(self):
        """Check and display current dashboard structure"""
        logger.section("Dashboard Structure")
        
        children = self.notion.get_block_children(self.dashboard_id)
        logger.info(f"Dashboard has {len(children)} child blocks")
        
        databases = []
        pages = []
        content = []
        
        for i, child in enumerate(children):
            block_type = child.get('type', 'unknown')
            block_id = child.get('id', 'no-id')
            
            if block_type == 'child_database':
                databases.append((i+1, block_id, "DATABASE"))
            elif block_type == 'child_page':
                pages.append((i+1, block_id, "PAGE"))
            else:
                content.append((i+1, block_type))
        
        if databases:
            logger.subsection("Child Databases")
            for pos, db_id, label in databases:
                logger.info(f"   {pos}. {label}: {db_id}")
        
        if pages:
            logger.subsection("Child Pages")
            for pos, page_id, label in pages:
                logger.info(f"   {pos}. {label}: {page_id}")
        
        logger.subsection("Content Blocks")
        logger.info(f"   {len(content)} content blocks (headings, text, etc.)")
        
        return {"databases": len(databases), "pages": len(pages), "content": len(content)}