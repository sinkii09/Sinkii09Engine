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
        self.repo_name = "Engine"
        
        self.headers = {}
        if self.token:
            self.headers['Authorization'] = f'token {self.token}'
    
    def _make_request(self, url: str, **kwargs) -> requests.Response:
        """Make API request with error handling"""
        try:
            response = requests.get(url, headers=self.headers, **kwargs)
            return response
        except requests.exceptions.RequestException as e:
            logger.error(f"GitHub API request failed: {e}")
            raise
    
    def get_repository_info(self) -> Optional[Dict[str, Any]]:
        """Get repository information"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}'
        response = self._make_request(url)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get repository info: {response.status_code}")
            return None
    
    def get_issues(self, state: str = "all") -> List[Dict[str, Any]]:
        """Get repository issues"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/issues'
        params = {'state': state}
        
        response = self._make_request(url, params=params)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get issues: {response.status_code}")
            return []
    
    def get_commits(self, limit: int = 10) -> List[Dict[str, Any]]:
        """Get recent commits"""
        url = f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/commits'
        params = {'per_page': limit}
        
        response = self._make_request(url, params=params)
        
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