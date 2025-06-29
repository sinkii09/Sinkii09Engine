#!/usr/bin/env python3
"""
Notion API client for Sinkii09 Engine automation
Provides clean interface to Notion API with proper error handling
"""
import requests
import json
from typing import Dict, List, Optional, Any, Union
from datetime import datetime

from .logger import logger
from .config import Config

class NotionText:
    """Helper class for creating Notion rich text objects"""
    
    @staticmethod
    def create(content: str, bold: bool = False, italic: bool = False, 
              color: Optional[str] = None, link: Optional[str] = None) -> Dict[str, Any]:
        """Create a properly formatted rich text object"""
        text_obj = {
            "type": "text",
            "text": {"content": content}
        }
        
        if link:
            text_obj["text"]["link"] = {"url": link}
        
        # Only add annotations if they're not default values
        annotations = {}
        if bold:
            annotations["bold"] = True
        if italic:
            annotations["italic"] = True
        if color and color != "default":
            annotations["color"] = color
            
        if annotations:
            text_obj["annotations"] = annotations
            
        return text_obj

class NotionClient:
    """Clean interface to Notion API"""
    
    def __init__(self, config: Config):
        self.config = config
        self.token = config.notion_token
        self.headers = {
            'Authorization': f'Bearer {self.token}',
            'Notion-Version': '2022-06-28',
            'Content-Type': 'application/json'
        }
    
    def _make_request(self, method: str, url: str, **kwargs) -> requests.Response:
        """Make API request with error handling"""
        try:
            response = requests.request(method, url, headers=self.headers, **kwargs)
            return response
        except requests.exceptions.RequestException as e:
            logger.error(f"API request failed: {e}")
            raise
    
    def get_page(self, page_id: str) -> Optional[Dict[str, Any]]:
        """Get page information"""
        url = f'https://api.notion.com/v1/pages/{page_id}'
        response = self._make_request('GET', url)
        
        if response.status_code == 200:
            return response.json()
        else:
            logger.warning(f"Failed to get page {page_id}: {response.status_code}")
            return None
    
    def get_block_children(self, block_id: str) -> List[Dict[str, Any]]:
        """Get all children of a block"""
        url = f'https://api.notion.com/v1/blocks/{block_id}/children'
        response = self._make_request('GET', url)
        
        if response.status_code == 200:
            return response.json().get('results', [])
        else:
            logger.warning(f"Failed to get block children: {response.status_code}")
            return []
    
    def delete_block(self, block_id: str) -> bool:
        """Delete a block"""
        url = f"https://api.notion.com/v1/blocks/{block_id}"
        response = self._make_request('DELETE', url)
        return response.status_code == 200
    
    def append_blocks(self, parent_id: str, blocks: List[Dict[str, Any]]) -> bool:
        """Append blocks to a parent"""
        url = f'https://api.notion.com/v1/blocks/{parent_id}/children'
        data = {"children": blocks}
        
        response = self._make_request('PATCH', url, json=data)
        
        if response.status_code == 200:
            return True
        else:
            logger.error(f"Failed to append blocks: {response.status_code}")
            if response.text:
                error_msg = json.loads(response.text).get('message', '')
                logger.error(f"Error details: {error_msg}")
            return False
    
    def clear_content_blocks(self, page_id: str, preserve_databases: bool = True) -> List[Dict[str, Any]]:
        """Clear content blocks while optionally preserving databases and child pages"""
        children = self.get_block_children(page_id)
        
        if preserve_databases:
            # Separate databases/pages from content
            preserved_items = []
            content_blocks = []
            
            for child in children:
                block_type = child.get('type', '')
                if block_type in ['child_database', 'child_page']:
                    preserved_items.append(child)
                else:
                    content_blocks.append(child)
            
            # Delete only content blocks
            if content_blocks:
                logger.info(f"Clearing {len(content_blocks)} content blocks (preserving {len(preserved_items)} databases/pages)")
                for block in content_blocks:
                    self.delete_block(block['id'])
                logger.success("Content cleared, databases/pages preserved")
            
            return preserved_items
        else:
            # Delete all blocks
            if children:
                logger.info(f"Clearing {len(children)} blocks")
                for child in children:
                    self.delete_block(child['id'])
                logger.success("All blocks cleared")
            
            return []
    
    def search_pages(self, query: str = "") -> List[Dict[str, Any]]:
        """Search for pages in workspace"""
        url = 'https://api.notion.com/v1/search'
        data = {}
        if query:
            data['query'] = query
        
        response = self._make_request('POST', url, json=data)
        
        if response.status_code == 200:
            return response.json().get('results', [])
        else:
            logger.warning(f"Search failed: {response.status_code}")
            return []
    
    def find_page_by_title(self, title: str) -> Optional[Dict[str, Any]]:
        """Find a page by its title"""
        results = self.search_pages()
        
        for item in results:
            if item.get('object') != 'page':
                continue
                
            props = item.get('properties', {})
            for prop_name, prop_data in props.items():
                if prop_data.get('type') == 'title' and prop_data.get('title'):
                    page_title = prop_data['title'][0]['text']['content']
                    if title.lower() in page_title.lower():
                        return item
        
        return None
    
    def create_page(self, parent_id: str, title: str, icon: Optional[str] = None, 
                   blocks: Optional[List[Dict[str, Any]]] = None) -> Optional[str]:
        """Create a new page"""
        url = 'https://api.notion.com/v1/pages'
        
        data = {
            "parent": {"page_id": parent_id},
            "properties": {
                "title": {
                    "title": [NotionText.create(title)]
                }
            }
        }
        
        if icon:
            data["icon"] = {"emoji": icon}
        
        if blocks:
            data["children"] = blocks
        
        response = self._make_request('POST', url, json=data)
        
        if response.status_code == 200:
            page_id = response.json()['id']
            logger.success(f"Created page: {title}")
            return page_id
        else:
            logger.error(f"Failed to create page {title}: {response.status_code}")
            return None
    
    def create_database(self, parent_id: str, title: str, icon: Optional[str] = None,
                       properties: Optional[Dict[str, Any]] = None) -> Optional[str]:
        """Create a new database"""
        url = 'https://api.notion.com/v1/databases'
        
        # Default properties if none provided
        if not properties:
            properties = {
                "Name": {"title": {}},
                "Status": {
                    "select": {
                        "options": [
                            {"name": "Not started", "color": "gray"},
                            {"name": "In progress", "color": "yellow"},
                            {"name": "Completed", "color": "green"}
                        ]
                    }
                },
                "Priority": {
                    "select": {
                        "options": [
                            {"name": "Low", "color": "gray"},
                            {"name": "Medium", "color": "yellow"},
                            {"name": "High", "color": "red"}
                        ]
                    }
                }
            }
        
        data = {
            "parent": {"page_id": parent_id},
            "title": [NotionText.create(title)],
            "properties": properties
        }
        
        if icon:
            data["icon"] = {"emoji": icon}
        
        response = self._make_request('POST', url, json=data)
        
        if response.status_code == 200:
            db_id = response.json()['id']
            logger.success(f"Created database: {title}")
            return db_id
        else:
            logger.error(f"Failed to create database {title}: {response.status_code}")
            if response.text:
                error_msg = json.loads(response.text).get('message', '')
                logger.error(f"Error details: {error_msg}")
            return None
    
    def update_page_title(self, page_id: str, new_title: str) -> bool:
        """Update page title"""
        url = f'https://api.notion.com/v1/pages/{page_id}'
        
        data = {
            "properties": {
                "title": {
                    "title": [NotionText.create(new_title)]
                }
            }
        }
        
        response = self._make_request('PATCH', url, json=data)
        return response.status_code == 200
    
    def get_database_entries(self, database_id: str) -> List[Dict[str, Any]]:
        """Get all entries from a database"""
        url = f'https://api.notion.com/v1/databases/{database_id}/query'
        response = self._make_request('POST', url)
        
        if response.status_code == 200:
            return response.json().get('results', [])
        else:
            logger.warning(f"Failed to get database entries: {response.status_code}")
            return []