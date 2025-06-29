#!/usr/bin/env python3
"""
GitHub synchronization for Sinkii09 Engine
Syncs GitHub issues to Notion databases
"""
from typing import Dict, List, Any, Optional
from datetime import datetime

import sys
from pathlib import Path
sys.path.append(str(Path(__file__).parent.parent))

from core import Config, NotionClient, GitHubClient, logger

class GitHubSyncManager:
    """Manages synchronization between GitHub and Notion"""
    
    def __init__(self, config: Config = None):
        self.config = config or Config()
        self.notion = NotionClient(self.config)
        self.github = GitHubClient(self.config)
        self.issues_db_id = self.config.workspace_pages.get('roadmap_db')
    
    def get_notion_issues(self) -> Dict[int, Dict[str, Any]]:
        """Get existing issues from Notion database"""
        if not self.issues_db_id:
            logger.error("Issues database ID not found in configuration")
            return {}
        
        entries = self.notion.get_database_entries(self.issues_db_id)
        notion_issues = {}
        
        for entry in entries:
            try:
                properties = entry.get('properties', {})
                github_id_prop = properties.get('GitHub ID', {})
                
                if github_id_prop.get('type') == 'number' and github_id_prop.get('number'):
                    github_id = int(github_id_prop['number'])
                    notion_issues[github_id] = {
                        'notion_id': entry['id'],
                        'entry': entry
                    }
            except (ValueError, TypeError, KeyError):
                continue
        
        logger.info(f"Found {len(notion_issues)} existing Notion issues")
        return notion_issues
    
    def get_github_issues(self) -> List[Dict[str, Any]]:
        """Get issues from GitHub"""
        issues = self.github.get_issues()
        logger.info(f"Found {len(issues)} GitHub issues")
        return issues
    
    def format_issue_for_notion(self, issue: Dict[str, Any]) -> Dict[str, Any]:
        """Format GitHub issue for Notion database"""
        # Extract labels
        labels = [label.get('name', '') for label in issue.get('labels', [])]
        
        # Format dates
        created_date = None
        updated_date = None
        
        try:
            if issue.get('created_at'):
                created_date = datetime.fromisoformat(issue['created_at'].replace('Z', '+00:00')).isoformat()
            if issue.get('updated_at'):
                updated_date = datetime.fromisoformat(issue['updated_at'].replace('Z', '+00:00')).isoformat()
        except ValueError:
            pass
        
        # Truncate body if too long
        body = issue.get('body', '') or ''
        if len(body) > 2000:
            body = body[:1997] + "..."
        
        properties = {
            "GitHub ID": {"number": issue.get('number', 0)},
            "Name": {
                "title": [
                    {
                        "type": "text",
                        "text": {"content": issue.get('title', 'Untitled')}
                    }
                ]
            },
            "Status": {
                "select": {
                    "name": "Open" if issue.get('state') == 'open' else "Closed"
                }
            },
            "Priority": {
                "select": {
                    "name": self._determine_priority(labels)
                }
            },
            "URL": {
                "url": issue.get('html_url', '')
            },
            "Labels": {
                "multi_select": [{"name": label} for label in labels[:5]]  # Limit to 5 labels
            },
            "Description": {
                "rich_text": [
                    {
                        "type": "text",
                        "text": {"content": body}
                    }
                ] if body else []
            }
        }
        
        # Add dates if available
        if created_date:
            properties["Created"] = {"date": {"start": created_date}}
        if updated_date:
            properties["Updated"] = {"date": {"start": updated_date}}
        
        return properties
    
    def _determine_priority(self, labels: List[str]) -> str:
        """Determine priority based on GitHub labels"""
        label_priority = {
            'critical': 'High',
            'high': 'High', 
            'priority: high': 'High',
            'bug': 'Medium',
            'enhancement': 'Medium',
            'feature': 'Medium',
            'documentation': 'Low',
            'good first issue': 'Low'
        }
        
        for label in labels:
            priority = label_priority.get(label.lower())
            if priority:
                return priority
        
        return 'Medium'  # Default priority
    
    def create_notion_issue(self, issue_data: Dict[str, Any]) -> Optional[str]:
        """Create a new issue in Notion database"""
        url = f'https://api.notion.com/v1/pages'
        
        data = {
            "parent": {"database_id": self.issues_db_id},
            "properties": issue_data
        }
        
        response = self.notion._make_request('POST', url, json=data)
        
        if response.status_code == 200:
            return response.json()['id']
        else:
            logger.error(f"Failed to create issue: {response.status_code}")
            return None
    
    def update_notion_issue(self, notion_id: str, issue_data: Dict[str, Any]) -> bool:
        """Update existing issue in Notion database"""
        url = f'https://api.notion.com/v1/pages/{notion_id}'
        
        data = {"properties": issue_data}
        
        response = self.notion._make_request('PATCH', url, json=data)
        
        if response.status_code == 200:
            return True
        else:
            logger.error(f"Failed to update issue: {response.status_code}")
            return False
    
    def sync_issues(self) -> Dict[str, int]:
        """Sync GitHub issues to Notion database"""
        logger.section("GitHub Issues Sync")
        
        if not self.issues_db_id:
            logger.error("Issues database not configured")
            return {"error": 1}
        
        # Get existing data
        logger.step("Fetching existing Notion issues")
        notion_issues = self.get_notion_issues()
        
        logger.step("Fetching GitHub issues")
        github_issues = self.get_github_issues()
        
        # Sync statistics
        stats = {
            "created": 0,
            "updated": 0,
            "skipped": 0,
            "errors": 0
        }
        
        logger.step("Synchronizing issues")
        
        for i, github_issue in enumerate(github_issues):
            github_id = github_issue.get('number')
            if not github_id:
                stats["errors"] += 1
                continue
            
            logger.progress(i + 1, len(github_issues), f"Issue #{github_id}")
            
            # Format issue data
            issue_data = self.format_issue_for_notion(github_issue)
            
            if github_id in notion_issues:
                # Update existing issue
                notion_id = notion_issues[github_id]['notion_id']
                if self.update_notion_issue(notion_id, issue_data):
                    stats["updated"] += 1
                    logger.info(f"Updated: Issue #{github_id}: {github_issue.get('title', '')[:50]}...")
                else:
                    stats["errors"] += 1
            else:
                # Create new issue
                notion_id = self.create_notion_issue(issue_data)
                if notion_id:
                    stats["created"] += 1
                    logger.success(f"Created: Issue #{github_id}: {github_issue.get('title', '')[:50]}...")
                else:
                    stats["errors"] += 1
        
        # Report results
        logger.subsection("Sync Results")
        logger.success(f"Created: {stats['created']} issues")
        logger.success(f"Updated: {stats['updated']} issues") 
        if stats['errors'] > 0:
            logger.error(f"Errors: {stats['errors']} issues")
        
        logger.success("GitHub to Notion sync completed")
        
        return stats
    
    def get_sync_status(self) -> Dict[str, Any]:
        """Get current sync status"""
        logger.section("GitHub Sync Status")
        
        if not self.issues_db_id:
            logger.error("Issues database not configured")
            return {"error": "Database not configured"}
        
        # Get counts
        notion_issues = self.get_notion_issues()
        github_issues = self.get_github_issues()
        
        # Count open/closed
        github_open = len([i for i in github_issues if i.get('state') == 'open'])
        github_closed = len(github_issues) - github_open
        
        status = {
            "github": {
                "total": len(github_issues),
                "open": github_open,
                "closed": github_closed
            },
            "notion": {
                "total": len(notion_issues)
            },
            "sync": {
                "in_sync": len(notion_issues) == len(github_issues),
                "missing_from_notion": len(github_issues) - len(notion_issues)
            }
        }
        
        logger.subsection("Status Summary")
        logger.info(f"GitHub: {status['github']['total']} issues ({status['github']['open']} open)")
        logger.info(f"Notion: {status['notion']['total']} issues")
        
        if status['sync']['in_sync']:
            logger.success("✅ Databases are in sync")
        else:
            missing = status['sync']['missing_from_notion']
            if missing > 0:
                logger.warning(f"⚠️  {missing} GitHub issues missing from Notion")
            else:
                logger.warning(f"⚠️  {abs(missing)} extra issues in Notion")
        
        return status