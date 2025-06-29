#!/usr/bin/env python3
"""
GitHub API client for Sinkii09 Engine automation
Provides clean interface to GitHub API for repository information
"""
import requests
from typing import Dict, List, Optional, Any
from datetime import datetime

from .logger import logger
from .config import Config

class GitHubClient:
    """Clean interface to GitHub API"""
    
    def __init__(self, config: Config):
        self.config = config
        self.token = config.github_token
        self.repo_owner = "Sinkii09"
        self.repo_name = "Sinkii09Engine"
        
        self.headers = {}
        if self.token:
            self.headers['Authorization'] = f'token {self.token}'
    
    def _make_request(self, method: str, url: str, **kwargs) -> requests.Response:
        """Make API request with error handling"""
        try:
            if method.upper() == 'GET':
                response = requests.get(url, headers=self.headers, **kwargs)
            elif method.upper() == 'POST':
                response = requests.post(url, headers=self.headers, **kwargs)
            elif method.upper() == 'PATCH':
                response = requests.patch(url, headers=self.headers, **kwargs)
            elif method.upper() == 'PUT':
                response = requests.put(url, headers=self.headers, **kwargs)
            elif method.upper() == 'DELETE':
                response = requests.delete(url, headers=self.headers, **kwargs)
            else:
                raise ValueError(f"Unsupported HTTP method: {method}")
            return response
        except requests.exceptions.RequestException as e:
            logger.error(f"GitHub API request failed: {e}")
            raise
    
    def get_repository_info(self) -> Optional[Dict[str, Any]]:
        """Get repository information"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}'
        response = self._make_request('GET', url)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get repository info: {response.status_code}")
            return None
    
    def get_issues(self, state: str = "all") -> List[Dict[str, Any]]:
        """Get repository issues"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/issues'
        params = {'state': state}
        
        response = self._make_request('GET', url, params=params)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get issues: {response.status_code}")
            return []
    
    def get_commits(self, limit: int = 10) -> List[Dict[str, Any]]:
        """Get recent commits"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/commits'
        params = {'per_page': limit}
        
        response = self._make_request('GET', url, params=params)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get commits: {response.status_code}")
            return []
    
    def get_repository_stats(self) -> Dict[str, Any]:
        """Get comprehensive repository statistics"""
        stats = {
            "open_issues": 0,
            "closed_issues": 0,
            "stars": 0,
            "forks": 0,
            "watchers": 0,
            "last_commit": "Unknown",
            "total_commits": 0
        }
        
        if not self.token:
            logger.info("Using mock GitHub stats (no token provided)")
            return {
                "open_issues": 8,
                "closed_issues": 12,
                "stars": 45,
                "forks": 3,
                "watchers": 15,
                "last_commit": "2 hours ago",
                "total_commits": 24
            }
        
        try:
            # Get repository info
            repo_info = self.get_repository_info()
            if repo_info:
                stats["stars"] = repo_info.get('stargazers_count', 0)
                stats["forks"] = repo_info.get('forks_count', 0)
                stats["watchers"] = repo_info.get('watchers_count', 0)
            
            # Get issues
            issues = self.get_issues()
            stats["open_issues"] = len([i for i in issues if i.get('state') == 'open'])
            stats["closed_issues"] = len([i for i in issues if i.get('state') == 'closed'])
            
            # Get latest commit info
            commits = self.get_commits(1)
            if commits:
                commit_date = datetime.strptime(
                    commits[0]['commit']['author']['date'], 
                    '%Y-%m-%dT%H:%M:%SZ'
                )
                time_diff = datetime.utcnow() - commit_date
                
                if time_diff.days > 0:
                    stats["last_commit"] = f"{time_diff.days} days ago"
                elif time_diff.seconds > 3600:
                    stats["last_commit"] = f"{time_diff.seconds // 3600} hours ago"
                else:
                    stats["last_commit"] = f"{time_diff.seconds // 60} minutes ago"
            
            logger.success("GitHub statistics fetched successfully")
            
        except Exception as e:
            logger.warning(f"Failed to fetch GitHub stats: {e}")
            logger.info("Using fallback statistics")
        
        return stats
    
    def create_issue(self, title: str, body: str = "", labels: List[str] = None, 
                    assignees: List[str] = None, milestone: str = None) -> Optional[Dict[str, Any]]:
        """Create a new GitHub issue"""
        if not self.token:
            logger.error("GitHub token required to create issues")
            return None
        
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/issues'
        
        data = {
            "title": title,
            "body": body
        }
        
        if labels:
            data["labels"] = labels
        if assignees:
            data["assignees"] = assignees
        if milestone:
            # For simplicity, we'll pass milestone as number if provided
            try:
                data["milestone"] = int(milestone)
            except ValueError:
                logger.warning(f"Milestone should be a number, got: {milestone}")
        
        response = self._make_request('POST', url, json=data)
        
        if response.status_code == 201:
            issue = response.json()
            logger.success(f"Created issue #{issue['number']}: {title}")
            return issue
        else:
            logger.error(f"Failed to create issue: {response.status_code} - {response.text}")
            return None
    
    def update_issue(self, issue_number: int, title: str = None, body: str = None, 
                    state: str = None, labels: List[str] = None) -> Optional[Dict[str, Any]]:
        """Update an existing GitHub issue"""
        if not self.token:
            logger.error("GitHub token required to update issues")
            return None
        
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/issues/{issue_number}'
        
        data = {}
        if title is not None:
            data["title"] = title
        if body is not None:
            data["body"] = body
        if state is not None:
            data["state"] = state
        if labels is not None:
            data["labels"] = labels
        
        if not data:
            logger.warning("No update data provided")
            return None
        
        response = self._make_request('PATCH', url, json=data)
        
        if response.status_code == 200:
            issue = response.json()
            logger.success(f"Updated issue #{issue['number']}: {issue['title']}")
            return issue
        else:
            logger.error(f"Failed to update issue: {response.status_code} - {response.text}")
            return None
    
    def get_issue(self, issue_number: int) -> Optional[Dict[str, Any]]:
        """Get a specific GitHub issue"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/issues/{issue_number}'
        response = self._make_request('GET', url)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get issue #{issue_number}: {response.status_code}")
            return None
    
    def add_issue_comment(self, issue_number: int, comment: str) -> Optional[Dict[str, Any]]:
        """Add a comment to a GitHub issue"""
        if not self.token:
            logger.error("GitHub token required to add comments")
            return None
        
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/issues/{issue_number}/comments'
        data = {"body": comment}
        
        response = self._make_request('POST', url, json=data)
        
        if response.status_code == 201:
            comment_data = response.json()
            logger.success(f"Added comment to issue #{issue_number}")
            return comment_data
        else:
            logger.error(f"Failed to add comment: {response.status_code} - {response.text}")
            return None
    
    def get_milestones(self) -> List[Dict[str, Any]]:
        """Get repository milestones"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/milestones'
        response = self._make_request('GET', url)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get milestones: {response.status_code}")
            return []
    
    def create_milestone(self, title: str, description: str = "", due_date: str = None) -> Optional[Dict[str, Any]]:
        """Create a new milestone"""
        if not self.token:
            logger.error("GitHub token required to create milestones")
            return None
        
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/milestones'
        
        data = {
            "title": title,
            "description": description
        }
        
        if due_date:
            data["due_on"] = due_date
        
        response = self._make_request('POST', url, json=data)
        
        if response.status_code == 201:
            milestone = response.json()
            logger.success(f"Created milestone: {title}")
            return milestone
        else:
            logger.error(f"Failed to create milestone: {response.status_code} - {response.text}")
            return None
    
    def format_issue_for_notion(self, issue: Dict[str, Any]) -> Dict[str, Any]:
        """Format GitHub issue for Notion database entry"""
        return {
            "GitHub ID": issue.get('number', 0),
            "Title": issue.get('title', 'Untitled'),
            "Status": "Open" if issue.get('state') == 'open' else "Closed",
            "URL": issue.get('html_url', ''),
            "Created": issue.get('created_at', ''),
            "Updated": issue.get('updated_at', ''),
            "Body": issue.get('body', '')[:2000] if issue.get('body') else '',  # Truncate long descriptions
            "Labels": [label.get('name', '') for label in issue.get('labels', [])]
        }