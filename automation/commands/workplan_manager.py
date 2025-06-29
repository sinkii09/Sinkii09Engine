#!/usr/bin/env python3
"""
Work Plan Manager for Sinkii09 Engine
Dynamic creation and management of work plans from markdown documents
"""
from typing import Dict, List, Any, Optional, Union
import json
from pathlib import Path
from datetime import datetime

import sys
from pathlib import Path
sys.path.append(str(Path(__file__).parent.parent))

from core import Config, GitHubClient, logger, WorkPlanParser, WorkPlanItem, ItemType, WorkPlanTemplate
from .github_sync import GitHubSyncManager

class WorkPlanManager:
    """Manages work plans with dynamic GitHub issue creation and Notion sync"""
    
    def __init__(self, config: Config = None):
        self.config = config or Config()
        self.github = GitHubClient(self.config)
        self.parser = WorkPlanParser()
        self.github_sync = GitHubSyncManager(self.config)
        self.templates_dir = Path(__file__).parent.parent / "workplans"
        self.templates_dir.mkdir(exist_ok=True)
    
    def create_workplan_from_file(self, file_path: Union[str, Path], 
                                 sync_to_notion: bool = True) -> Dict[str, Any]:
        """Create complete work plan (epics, issues, tasks) from markdown file"""
        logger.section("🚀 Creating Work Plan from File")
        
        file_path = Path(file_path)
        
        # Parse the work plan file
        logger.step("Parsing work plan document")
        items = self.parser.parse_file(file_path)
        
        if not items:
            logger.error("No work plan items found in file")
            return {"success": False, "error": "No items parsed"}
        
        # Create GitHub issues
        logger.step("Creating GitHub issues")
        results = self._create_github_issues(items)
        
        # Sync to Notion if requested
        if sync_to_notion and results["success"]:
            logger.step("Syncing to Notion")
            sync_result = self.github_sync.sync_issues()
            results["notion_sync"] = sync_result
        
        # Save work plan state
        self._save_workplan_state(file_path, items, results)
        
        if results["success"]:
            logger.success(f"🎉 Work plan created successfully!")
            logger.info(f"Created {results['stats']['total_created']} GitHub issues")
            if sync_to_notion:
                logger.info("✅ Synced to Notion")
        
        return results
    
    def update_workplan_from_file(self, file_path: Union[str, Path], 
                                 sync_to_notion: bool = True) -> Dict[str, Any]:
        """Update existing work plan from modified markdown file"""
        logger.section("🔄 Updating Work Plan from File")
        
        file_path = Path(file_path)
        
        # Load existing state
        existing_state = self._load_workplan_state(file_path)
        
        # Parse the updated work plan file
        logger.step("Parsing updated work plan document")
        new_items = self.parser.parse_file(file_path)
        
        if not new_items:
            logger.error("No work plan items found in updated file")
            return {"success": False, "error": "No items parsed"}
        
        # Compare and update GitHub issues
        logger.step("Comparing and updating GitHub issues")
        results = self._update_github_issues(existing_state, new_items)
        
        # Sync to Notion if requested
        if sync_to_notion and results["success"]:
            logger.step("Syncing updates to Notion")
            sync_result = self.github_sync.sync_issues()
            results["notion_sync"] = sync_result
        
        # Save updated work plan state
        self._save_workplan_state(file_path, new_items, results)
        
        if results["success"]:
            logger.success(f"🎉 Work plan updated successfully!")
            logger.info(f"Updated {results['stats']['updated']} issues")
            logger.info(f"Created {results['stats']['created']} new issues")
            if sync_to_notion:
                logger.info("✅ Synced to Notion")
        
        return results
    
    def generate_workplan_template(self, template_type: str, output_path: Union[str, Path]) -> bool:
        """Generate a work plan template file"""
        logger.section(f"📝 Generating Work Plan Template: {template_type}")
        
        output_path = Path(output_path)
        
        # Get template content
        if template_type == "basic":
            content = WorkPlanTemplate.create_basic_epic_template()
        elif template_type == "service":
            content = WorkPlanTemplate.create_service_enhancement_template()
        else:
            logger.error(f"Unknown template type: {template_type}")
            return False
        
        # Create directories if needed
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        # Write template file
        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                f.write(content)
            
            logger.success(f"Template created: {output_path}")
            logger.info("You can now edit this file and create a work plan from it")
            return True
            
        except Exception as e:
            logger.error(f"Failed to create template: {e}")
            return False
    
    def _create_github_issues(self, items: List[WorkPlanItem]) -> Dict[str, Any]:
        """Create GitHub issues from work plan items"""
        results = {
            "success": True,
            "created_issues": [],
            "failed_issues": [],
            "stats": {
                "total_created": 0,
                "epics": 0,
                "issues": 0,
                "tasks": 0,
                "failed": 0
            }
        }
        
        # First pass: Create all issues
        all_items = self._flatten_items(items)
        
        for item in all_items:
            github_issue = self._create_single_github_issue(item)
            
            if github_issue:
                item.github_number = github_issue["number"]
                results["created_issues"].append({
                    "item": item,
                    "github_issue": github_issue
                })
                results["stats"]["total_created"] += 1
                results["stats"][item.item_type.value + "s"] += 1
                
                logger.info(f"✅ Created {item.item_type.value}: #{github_issue['number']} - {item.title}")
            else:
                results["failed_issues"].append(item)
                results["stats"]["failed"] += 1
                results["success"] = False
                
                logger.error(f"❌ Failed to create {item.item_type.value}: {item.title}")
        
        # Second pass: Add cross-references and links
        self._add_issue_cross_references(items, results["created_issues"])
        
        return results
    
    def _update_github_issues(self, existing_state: Dict[str, Any], 
                             new_items: List[WorkPlanItem]) -> Dict[str, Any]:
        """Update GitHub issues based on changes in work plan"""
        results = {
            "success": True,
            "updated_issues": [],
            "created_issues": [],
            "stats": {
                "updated": 0,
                "created": 0,
                "failed": 0
            }
        }
        
        # Build mapping of existing issues
        existing_issues = {}
        if existing_state:
            for item_data in existing_state.get("items", []):
                if item_data.get("github_number"):
                    existing_issues[item_data["title"]] = item_data
        
        # Process all items
        all_items = self._flatten_items(new_items)
        
        for item in all_items:
            if item.title in existing_issues:
                # Update existing issue
                existing_item = existing_issues[item.title]
                item.github_number = existing_item["github_number"]
                
                if self._needs_update(existing_item, item):
                    updated_issue = self._update_single_github_issue(item)
                    if updated_issue:
                        results["updated_issues"].append({
                            "item": item,
                            "github_issue": updated_issue
                        })
                        results["stats"]["updated"] += 1
                        logger.info(f"✅ Updated: #{item.github_number} - {item.title}")
                    else:
                        results["stats"]["failed"] += 1
                        results["success"] = False
                        logger.error(f"❌ Failed to update: {item.title}")
            else:
                # Create new issue
                github_issue = self._create_single_github_issue(item)
                if github_issue:
                    item.github_number = github_issue["number"]
                    results["created_issues"].append({
                        "item": item,
                        "github_issue": github_issue
                    })
                    results["stats"]["created"] += 1
                    logger.info(f"✅ Created: #{github_issue['number']} - {item.title}")
                else:
                    results["stats"]["failed"] += 1
                    results["success"] = False
                    logger.error(f"❌ Failed to create: {item.title}")
        
        return results
    
    def _create_single_github_issue(self, item: WorkPlanItem) -> Optional[Dict[str, Any]]:
        """Create a single GitHub issue from work plan item"""
        # Build issue body
        body = self._build_issue_body(item)
        
        # Determine labels
        labels = item.labels.copy()
        labels.append(item.item_type.value)
        if item.priority and item.priority != "medium":
            labels.append(f"priority-{item.priority}")
        
        # Create the issue
        return self.github.create_issue(
            title=item.title,
            body=body,
            labels=labels,
            assignees=item.assignees
        )
    
    def _update_single_github_issue(self, item: WorkPlanItem) -> Optional[Dict[str, Any]]:
        """Update a single GitHub issue from work plan item"""
        # Build updated issue body
        body = self._build_issue_body(item)
        
        # Determine labels
        labels = item.labels.copy()
        labels.append(item.item_type.value)
        if item.priority and item.priority != "medium":
            labels.append(f"priority-{item.priority}")
        
        # Update the issue
        return self.github.update_issue(
            issue_number=item.github_number,
            title=item.title,
            body=body,
            labels=labels
        )
    
    def _build_issue_body(self, item: WorkPlanItem) -> str:
        """Build GitHub issue body from work plan item"""
        body_parts = []
        
        # Description
        if item.description:
            body_parts.append(f"## Description\n{item.description}")
        
        # Properties
        properties = []
        if item.priority:
            properties.append(f"**Priority**: {item.priority.title()}")
        if item.estimated_effort:
            properties.append(f"**Estimated Effort**: {item.estimated_effort}")
        if item.milestone:
            properties.append(f"**Milestone**: {item.milestone}")
        
        if properties:
            body_parts.append("## Properties\n" + "\n".join(properties))
        
        # Acceptance Criteria
        if item.acceptance_criteria:
            criteria_text = "\n".join([f"- [ ] {criterion}" for criterion in item.acceptance_criteria])
            body_parts.append(f"## Acceptance Criteria\n{criteria_text}")
        
        # Deliverables
        if item.deliverables:
            deliverables_text = "\n".join([f"- [ ] {deliverable}" for deliverable in item.deliverables])
            body_parts.append(f"## Deliverables\n{deliverables_text}")
        
        # Dependencies
        if item.dependencies:
            deps_text = "\n".join([f"- {dep}" for dep in item.dependencies])
            body_parts.append(f"## Dependencies\n{deps_text}")
        
        # Sub-items (for epics and issues)
        if item.sub_items:
            sub_items_text = "\n".join([
                f"- [ ] {sub_item.title}" + 
                (f" (#{sub_item.github_number})" if sub_item.github_number else "")
                for sub_item in item.sub_items
            ])
            body_parts.append(f"## Sub-Items\n{sub_items_text}")
        
        # Add automation signature
        body_parts.append("\n---\n*This issue was created/updated automatically from a work plan document.*")
        
        return "\n\n".join(body_parts)
    
    def _flatten_items(self, items: List[WorkPlanItem]) -> List[WorkPlanItem]:
        """Flatten hierarchical items into a single list"""
        flat_items = []
        
        def add_item_and_children(item: WorkPlanItem):
            flat_items.append(item)
            for sub_item in item.sub_items:
                add_item_and_children(sub_item)
        
        for item in items:
            add_item_and_children(item)
        
        return flat_items
    
    def _add_issue_cross_references(self, items: List[WorkPlanItem], created_issues: List[Dict[str, Any]]):
        """Add cross-references between related issues"""
        # Build mapping of items to GitHub issues
        item_to_issue = {}
        for created in created_issues:
            item = created["item"]
            github_issue = created["github_issue"]
            item_to_issue[item] = github_issue
        
        # Add comments linking parent and child issues
        def add_references(item: WorkPlanItem):
            if item in item_to_issue:
                parent_issue = item_to_issue[item]
                
                # Add comments for sub-items
                for sub_item in item.sub_items:
                    if sub_item in item_to_issue:
                        child_issue = item_to_issue[sub_item]
                        
                        # Add comment to parent linking child
                        self.github.add_issue_comment(
                            parent_issue["number"],
                            f"Sub-item: #{child_issue['number']} - {child_issue['title']}"
                        )
                        
                        # Add comment to child linking parent
                        self.github.add_issue_comment(
                            child_issue["number"],
                            f"Part of: #{parent_issue['number']} - {parent_issue['title']}"
                        )
                
                # Recursively process sub-items
                for sub_item in item.sub_items:
                    add_references(sub_item)
        
        for item in items:
            add_references(item)
    
    def _needs_update(self, existing_item: Dict[str, Any], new_item: WorkPlanItem) -> bool:
        """Check if an item needs to be updated"""
        # Simple comparison - in practice, you might want more sophisticated logic
        return (
            existing_item.get("description") != new_item.description or
            existing_item.get("labels") != new_item.labels or
            existing_item.get("priority") != new_item.priority or
            existing_item.get("estimated_effort") != new_item.estimated_effort or
            existing_item.get("acceptance_criteria") != new_item.acceptance_criteria or
            existing_item.get("deliverables") != new_item.deliverables or
            existing_item.get("dependencies") != new_item.dependencies
        )
    
    def _save_workplan_state(self, file_path: Path, items: List[WorkPlanItem], 
                            results: Dict[str, Any]) -> None:
        """Save work plan state for future updates"""
        state_file = file_path.with_suffix('.workplan_state.json')
        
        state = {
            "file_path": str(file_path),
            "created_at": datetime.now().isoformat(),
            "items": [item.to_dict() for item in self._flatten_items(items)],
            "results": results
        }
        
        try:
            with open(state_file, 'w', encoding='utf-8') as f:
                json.dump(state, f, indent=2, ensure_ascii=False)
        except Exception as e:
            logger.warning(f"Failed to save work plan state: {e}")
    
    def _load_workplan_state(self, file_path: Path) -> Optional[Dict[str, Any]]:
        """Load existing work plan state"""
        state_file = file_path.with_suffix('.workplan_state.json')
        
        if not state_file.exists():
            return None
        
        try:
            with open(state_file, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            logger.warning(f"Failed to load work plan state: {e}")
            return None
    
    def list_workplans(self) -> List[Dict[str, Any]]:
        """List all available work plans"""
        workplans = []
        
        # Look for markdown files in templates directory
        for md_file in self.templates_dir.glob("*.md"):
            state_file = md_file.with_suffix('.workplan_state.json')
            
            workplan_info = {
                "file": str(md_file),
                "name": md_file.stem,
                "has_state": state_file.exists(),
                "last_modified": datetime.fromtimestamp(md_file.stat().st_mtime).isoformat()
            }
            
            if state_file.exists():
                try:
                    with open(state_file, 'r') as f:
                        state = json.load(f)
                        workplan_info["created_at"] = state.get("created_at")
                        workplan_info["items_count"] = len(state.get("items", []))
                except:
                    pass
            
            workplans.append(workplan_info)
        
        return workplans
    
    def get_workplan_status(self, file_path: Union[str, Path]) -> Dict[str, Any]:
        """Get status of a specific work plan"""
        file_path = Path(file_path)
        state = self._load_workplan_state(file_path)
        
        if not state:
            return {"exists": False}
        
        # Count different types of items
        items = state.get("items", [])
        status = {
            "exists": True,
            "file_path": str(file_path),
            "created_at": state.get("created_at"),
            "total_items": len(items),
            "epics": len([i for i in items if i.get("type") == "epic"]),
            "issues": len([i for i in items if i.get("type") == "issue"]), 
            "tasks": len([i for i in items if i.get("type") == "task"]),
            "github_issues": len([i for i in items if i.get("github_number")]),
            "last_results": state.get("results", {})
        }
        
        return status