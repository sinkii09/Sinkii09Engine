#!/usr/bin/env python3
"""
Work Plan Parser for Sinkii09 Engine
Parses markdown documents to extract epics, issues, and tasks dynamically
"""
import re
import yaml
from typing import Dict, List, Any, Optional, Union
from pathlib import Path
from dataclasses import dataclass, field
from enum import Enum

from .logger import logger

class ItemType(Enum):
    EPIC = "epic"
    ISSUE = "issue"
    TASK = "task"
    SUBTASK = "subtask"

@dataclass
class WorkPlanItem:
    """Represents a work plan item (epic, issue, or task)"""
    title: str
    item_type: ItemType
    description: str = ""
    labels: List[str] = field(default_factory=list)
    assignees: List[str] = field(default_factory=list)
    milestone: Optional[str] = None
    priority: str = "medium"
    estimated_effort: Optional[str] = None
    dependencies: List[str] = field(default_factory=list)
    acceptance_criteria: List[str] = field(default_factory=list)
    deliverables: List[str] = field(default_factory=list)
    sub_items: List['WorkPlanItem'] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)
    github_number: Optional[int] = None
    notion_id: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization"""
        return {
            "title": self.title,
            "type": self.item_type.value,
            "description": self.description,
            "labels": self.labels,
            "assignees": self.assignees,
            "milestone": self.milestone,
            "priority": self.priority,
            "estimated_effort": self.estimated_effort,
            "dependencies": self.dependencies,
            "acceptance_criteria": self.acceptance_criteria,
            "deliverables": self.deliverables,
            "sub_items": [item.to_dict() for item in self.sub_items],
            "metadata": self.metadata,
            "github_number": self.github_number,
            "notion_id": self.notion_id
        }

class WorkPlanParser:
    """Parses markdown documents to extract work plan structure"""
    
    def __init__(self):
        # Regex patterns for parsing
        self.header_pattern = re.compile(r'^(#{1,6})\s+(.+)$', re.MULTILINE)
        self.metadata_pattern = re.compile(r'^---\n(.*?)\n---', re.DOTALL)
        self.list_item_pattern = re.compile(r'^(\s*)-\s+(.+)$', re.MULTILINE)
        self.checkbox_pattern = re.compile(r'^(\s*)-\s+\[[ x]\]\s+(.+)$', re.MULTILINE)
        self.property_pattern = re.compile(r'^\*\*(.+?)\*\*:\s*(.+)$', re.MULTILINE)
        
        # Keywords for identifying different sections
        self.epic_keywords = ['epic', 'project', 'initiative', 'feature set']
        self.task_keywords = ['task', 'story', 'work item', 'todo']
        self.criteria_keywords = ['acceptance criteria', 'success criteria', 'completion criteria']
        self.deliverable_keywords = ['deliverables', 'outputs', 'artifacts']
        self.dependency_keywords = ['dependencies', 'requires', 'depends on', 'prerequisites']
    
    def parse_file(self, file_path: Union[str, Path]) -> List[WorkPlanItem]:
        """Parse a markdown file and extract work plan items"""
        file_path = Path(file_path)
        
        if not file_path.exists():
            logger.error(f"Work plan file not found: {file_path}")
            return []
        
        logger.step(f"Parsing work plan: {file_path.name}")
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except Exception as e:
            logger.error(f"Failed to read file: {e}")
            return []
        
        return self.parse_content(content)
    
    def parse_content(self, content: str) -> List[WorkPlanItem]:
        """Parse markdown content and extract work plan items"""
        # Extract metadata if present
        metadata = self._extract_metadata(content)
        
        # Remove metadata from content
        content = self.metadata_pattern.sub('', content).strip()
        
        # Split content into sections by headers
        sections = self._split_into_sections(content)
        
        # Parse each section
        items = []
        for section in sections:
            item = self._parse_section(section, metadata)
            if item:
                items.append(item)
        
        # Build hierarchy
        items = self._build_hierarchy(items)
        
        logger.success(f"Parsed {len(items)} work plan items")
        return items
    
    def _extract_metadata(self, content: str) -> Dict[str, Any]:
        """Extract YAML metadata from the beginning of the document"""
        match = self.metadata_pattern.search(content)
        if not match:
            return {}
        
        try:
            metadata = yaml.safe_load(match.group(1))
            return metadata or {}
        except yaml.YAMLError as e:
            logger.warning(f"Failed to parse metadata: {e}")
            return {}
    
    def _split_into_sections(self, content: str) -> List[Dict[str, Any]]:
        """Split content into sections based on headers"""
        sections = []
        lines = content.split('\n')
        current_section = None
        
        for line in lines:
            header_match = self.header_pattern.match(line)
            if header_match:
                # Save previous section
                if current_section:
                    sections.append(current_section)
                
                # Start new section
                level = len(header_match.group(1))
                title = header_match.group(2).strip()
                current_section = {
                    'level': level,
                    'title': title,
                    'content': []
                }
            elif current_section:
                current_section['content'].append(line)
        
        # Add last section
        if current_section:
            sections.append(current_section)
        
        return sections
    
    def _parse_section(self, section: Dict[str, Any], global_metadata: Dict[str, Any]) -> Optional[WorkPlanItem]:
        """Parse a single section into a WorkPlanItem"""
        title = section['title']
        content = '\n'.join(section['content']).strip()
        level = section['level']
        
        # Determine item type based on level and keywords
        item_type = self._determine_item_type(title, level)
        
        # Skip sections that are not actionable items
        if item_type is None:
            return None
        
        # Extract properties from content
        properties = self._extract_properties(content)
        
        # Create work plan item
        item = WorkPlanItem(
            title=title,
            item_type=item_type,
            description=properties.get('description', ''),
            labels=self._parse_list(properties.get('labels', '')),
            assignees=self._parse_list(properties.get('assignees', '')),
            milestone=properties.get('milestone'),
            priority=properties.get('priority', 'medium'),
            estimated_effort=properties.get('effort', properties.get('estimated_effort')),
            dependencies=self._extract_dependencies(content),
            acceptance_criteria=self._extract_criteria(content),
            deliverables=self._extract_deliverables(content),
            metadata={**global_metadata, **properties}
        )
        
        return item
    
    def _determine_item_type(self, title: str, level: int) -> ItemType:
        """Determine the type of work plan item based on title and level"""
        title_lower = title.lower()
        
        # Check for explicit Epic keywords (level 1)
        if level == 1 and any(keyword in title_lower for keyword in self.epic_keywords):
            return ItemType.EPIC
        
        # Check for explicit Issue keywords (level 3)
        if level == 3 and ('issue' in title_lower or 'task' in title_lower):
            return ItemType.ISSUE
        
        # Skip documentation sections (level 2 and 4+ headers)
        # These are content sections, not actionable items
        return None
    
    def _extract_properties(self, content: str) -> Dict[str, str]:
        """Extract properties from content using **Property**: Value format"""
        properties = {}
        
        for match in self.property_pattern.finditer(content):
            key = match.group(1).lower().replace(' ', '_')
            value = match.group(2).strip()
            properties[key] = value
        
        # Extract description (first paragraph that's not a property)
        lines = content.split('\n')
        description_lines = []
        
        for line in lines:
            line = line.strip()
            if not line:
                continue
            if self.property_pattern.match(line):
                continue
            if line.startswith('#'):
                continue
            if line.startswith('-'):
                break
            description_lines.append(line)
        
        if description_lines:
            properties['description'] = ' '.join(description_lines)
        
        return properties
    
    def _extract_dependencies(self, content: str) -> List[str]:
        """Extract dependencies from content"""
        dependencies = []
        
        # Look for dependency sections
        lines = content.split('\n')
        in_deps_section = False
        
        for line in lines:
            line = line.strip()
            
            # Check for dependency section headers
            if any(keyword in line.lower() for keyword in self.dependency_keywords):
                in_deps_section = True
                continue
            
            # If we're in a dependency section, extract list items
            if in_deps_section:
                if line.startswith('-'):
                    dep = line[1:].strip()
                    if dep:
                        dependencies.append(dep)
                elif line and not line.startswith(' '):
                    # End of dependency section
                    in_deps_section = False
        
        return dependencies
    
    def _extract_criteria(self, content: str) -> List[str]:
        """Extract acceptance criteria from content"""
        criteria = []
        
        # Look for criteria sections
        lines = content.split('\n')
        in_criteria_section = False
        
        for line in lines:
            line = line.strip()
            
            # Check for criteria section headers
            if any(keyword in line.lower() for keyword in self.criteria_keywords):
                in_criteria_section = True
                continue
            
            # If we're in a criteria section, extract list items or checkboxes
            if in_criteria_section:
                if line.startswith('-') or '[ ]' in line or '[x]' in line:
                    # Clean up the criterion
                    criterion = re.sub(r'^-\s*', '', line)
                    criterion = re.sub(r'\[[ x]\]\s*', '', criterion)
                    if criterion:
                        criteria.append(criterion.strip())
                elif line and not line.startswith(' '):
                    # End of criteria section
                    in_criteria_section = False
        
        return criteria
    
    def _extract_deliverables(self, content: str) -> List[str]:
        """Extract deliverables from content"""
        deliverables = []
        
        # Look for deliverable sections
        lines = content.split('\n')
        in_deliverables_section = False
        
        for line in lines:
            line = line.strip()
            
            # Check for deliverable section headers
            if any(keyword in line.lower() for keyword in self.deliverable_keywords):
                in_deliverables_section = True
                continue
            
            # If we're in a deliverables section, extract list items
            if in_deliverables_section:
                if line.startswith('-'):
                    deliverable = line[1:].strip()
                    if deliverable:
                        deliverables.append(deliverable)
                elif line and not line.startswith(' '):
                    # End of deliverables section
                    in_deliverables_section = False
        
        return deliverables
    
    def _parse_list(self, value: str) -> List[str]:
        """Parse a comma-separated list"""
        if not value:
            return []
        return [item.strip() for item in value.split(',') if item.strip()]
    
    def _build_hierarchy(self, items: List[WorkPlanItem]) -> List[WorkPlanItem]:
        """Build hierarchical structure from flat list of items"""
        if not items:
            return []
        
        # Create a stack to track parent items
        hierarchy = []
        stack = []
        
        for item in items:
            # Find the appropriate parent level
            while stack and not self._should_be_parent(stack[-1], item):
                stack.pop()
            
            if stack:
                # Add as sub-item to parent
                stack[-1].sub_items.append(item)
            else:
                # Top-level item
                hierarchy.append(item)
            
            # Add to stack if it can have children
            if item.item_type in [ItemType.EPIC, ItemType.ISSUE]:
                stack.append(item)
        
        return hierarchy
    
    def _should_be_parent(self, potential_parent: WorkPlanItem, child: WorkPlanItem) -> bool:
        """Determine if one item should be parent of another"""
        parent_types = {
            ItemType.EPIC: [ItemType.ISSUE, ItemType.TASK, ItemType.SUBTASK],
            ItemType.ISSUE: [ItemType.TASK, ItemType.SUBTASK],
            ItemType.TASK: [ItemType.SUBTASK]
        }
        
        return child.item_type in parent_types.get(potential_parent.item_type, [])

class WorkPlanTemplate:
    """Generates work plan templates for different scenarios"""
    
    @staticmethod
    def create_basic_epic_template() -> str:
        """Create a basic epic template"""
        return """---
milestone: "Sprint 1"
assignee: "@me"
default_labels: ["enhancement", "core"]
---

# Epic: [Epic Title]

**Description**: Brief description of the epic and its goals.

**Priority**: High
**Estimated Effort**: 8-10 story points
**Milestone**: Sprint 1

## Overview
Detailed overview of what this epic aims to accomplish.

## Acceptance Criteria
- [ ] Criterion 1: Clear, testable condition
- [ ] Criterion 2: Another measurable outcome
- [ ] Criterion 3: Final validation requirement

## Issues

### Issue 1: [Issue Title]

**Description**: Description of the first issue.
**Labels**: task, analysis
**Estimated Effort**: 2 days

#### Acceptance Criteria
- [ ] Specific deliverable 1
- [ ] Specific deliverable 2

#### Dependencies
- None (starting task)

### Issue 2: [Issue Title]

**Description**: Description of the second issue.
**Labels**: task, implementation
**Estimated Effort**: 3 days

#### Acceptance Criteria
- [ ] Implementation complete
- [ ] Tests passing

#### Dependencies
- Requires completion of Issue 1

### Issue 3: [Issue Title]

**Description**: Description of the third issue.
**Labels**: task, testing
**Estimated Effort**: 1 day

#### Acceptance Criteria
- [ ] All tests passing
- [ ] Documentation updated

#### Dependencies
- Requires completion of Issue 2

## Deliverables
- [ ] Feature implementation
- [ ] Test suite
- [ ] Documentation
- [ ] Performance benchmarks

## Success Metrics
- Code coverage > 90%
- Performance benchmarks met
- All acceptance criteria completed
"""

    @staticmethod
    def create_service_enhancement_template() -> str:
        """Create a template for service enhancement work"""
        return """---
milestone: "Enhanced Service Architecture"
default_labels: ["enhancement", "core", "service"]
priority: "high"
---

# Epic: Enhanced [Service Name] Implementation

**Description**: Complete enhancement of the [Service Name] with modern patterns and improved architecture.

**Priority**: High
**Estimated Effort**: 10-12 story points
**Milestone**: Enhanced Service Architecture

## Current State Analysis
- ✅ Basic implementation exists
- ⚠️ Missing feature X
- ⚠️ Limited feature Y
- ❌ Feature Z not implemented

## Enhancement Goals
1. Add missing functionality
2. Improve performance and reliability
3. Enhance error handling
4. Add comprehensive testing

## Issues

### Issue 1: Analysis and Requirements

**Description**: Analyze current implementation and define enhancement requirements.
**Labels**: task, analysis, documentation
**Estimated Effort**: 1-2 days

#### Acceptance Criteria
- [ ] Current implementation analyzed and documented
- [ ] Enhancement requirements specified
- [ ] Architecture design approved

#### Deliverables
- [ ] Analysis document
- [ ] Requirements specification
- [ ] Architecture design

### Issue 2: Core Implementation

**Description**: Implement the core enhancements to the service.
**Labels**: task, implementation, core
**Estimated Effort**: 4-5 days

#### Acceptance Criteria
- [ ] Core functionality implemented
- [ ] Interface enhancements complete
- [ ] Integration tests passing

#### Dependencies
- Requires completion of Issue 1

#### Deliverables
- [ ] Enhanced service implementation
- [ ] Updated interfaces
- [ ] Integration test suite

### Issue 3: Testing and Validation

**Description**: Comprehensive testing and validation of the enhanced service.
**Labels**: task, testing, validation
**Estimated Effort**: 2-3 days

#### Acceptance Criteria
- [ ] Unit tests cover 90%+ of new code
- [ ] Integration tests verify functionality
- [ ] Performance benchmarks meet requirements

#### Dependencies
- Requires completion of Issue 2

#### Deliverables
- [ ] Complete test suite
- [ ] Performance benchmark report
- [ ] Validation documentation

## Success Criteria
- [ ] All issues completed successfully
- [ ] Enhanced service passes all tests
- [ ] Performance improvements demonstrated
- [ ] Documentation updated and complete

## Deliverables
- [ ] Enhanced service implementation
- [ ] Comprehensive test suite
- [ ] Performance benchmarks
- [ ] Updated documentation
- [ ] Migration guide (if needed)
"""