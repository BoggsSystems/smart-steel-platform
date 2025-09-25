#!/usr/bin/env python3
import json
import os
import shutil
from datetime import datetime
from typing import Dict, List, Optional, Any
import joblib
import hashlib

class ModelVersionManager:
    """Manages ML model versions with metadata and rollback capabilities"""
    
    def __init__(self, models_dir: str = "./models", versions_dir: str = "./model_versions"):
        self.models_dir = models_dir
        self.versions_dir = versions_dir
        self.metadata_file = os.path.join(versions_dir, "model_metadata.json")
        
        os.makedirs(models_dir, exist_ok=True)
        os.makedirs(versions_dir, exist_ok=True)
        
        self.metadata = self._load_metadata()
    
    def _load_metadata(self) -> Dict[str, Any]:
        """Load model metadata from disk"""
        if os.path.exists(self.metadata_file):
            with open(self.metadata_file, 'r') as f:
                return json.load(f)
        return {"models": {}, "current_versions": {}}
    
    def _save_metadata(self):
        """Save model metadata to disk"""
        with open(self.metadata_file, 'w') as f:
            json.dump(self.metadata, f, indent=2, default=str)
    
    def _calculate_model_hash(self, model_path: str) -> str:
        """Calculate hash of model file for integrity checking"""
        hash_md5 = hashlib.md5()
        with open(model_path, "rb") as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hash_md5.update(chunk)
        return hash_md5.hexdigest()
    
    def register_model(
        self,
        model_name: str,
        model_path: str,
        metrics: Dict[str, float],
        training_data_info: Dict[str, Any],
        hyperparameters: Dict[str, Any],
        notes: str = ""
    ) -> str:
        """Register a new model version"""
        
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"Model file not found: {model_path}")
        
        # Generate version ID
        timestamp = datetime.utcnow().strftime("%Y%m%d_%H%M%S")
        version_id = f"{model_name}_v{timestamp}"
        
        # Create version directory
        version_dir = os.path.join(self.versions_dir, version_id)
        os.makedirs(version_dir, exist_ok=True)
        
        # Copy model file to version directory
        version_model_path = os.path.join(version_dir, f"{model_name}.pkl")
        shutil.copy2(model_path, version_model_path)
        
        # Calculate model hash
        model_hash = self._calculate_model_hash(version_model_path)
        
        # Store metadata
        if model_name not in self.metadata["models"]:
            self.metadata["models"][model_name] = []
        
        version_metadata = {
            "version_id": version_id,
            "created_at": datetime.utcnow().isoformat(),
            "model_path": version_model_path,
            "model_hash": model_hash,
            "metrics": metrics,
            "training_data_info": training_data_info,
            "hyperparameters": hyperparameters,
            "notes": notes,
            "is_active": False
        }
        
        self.metadata["models"][model_name].append(version_metadata)
        self._save_metadata()
        
        print(f"Registered model version: {version_id}")
        return version_id
    
    def promote_version(self, model_name: str, version_id: str):
        """Promote a model version to production (current)"""
        
        if model_name not in self.metadata["models"]:
            raise ValueError(f"Model {model_name} not found")
        
        # Find the version
        version_found = False
        for version in self.metadata["models"][model_name]:
            if version["version_id"] == version_id:
                version["is_active"] = True
                version_found = True
            else:
                version["is_active"] = False
        
        if not version_found:
            raise ValueError(f"Version {version_id} not found for model {model_name}")
        
        # Update current version
        self.metadata["current_versions"][model_name] = version_id
        
        # Copy to production location
        version_path = os.path.join(self.versions_dir, version_id, f"{model_name}.pkl")
        production_path = os.path.join(self.models_dir, f"{model_name}_model.pkl")
        shutil.copy2(version_path, production_path)
        
        self._save_metadata()
        print(f"Promoted {version_id} to production for {model_name}")
    
    def get_model_versions(self, model_name: str) -> List[Dict[str, Any]]:
        """Get all versions of a model"""
        if model_name not in self.metadata["models"]:
            return []
        
        return sorted(
            self.metadata["models"][model_name],
            key=lambda x: x["created_at"],
            reverse=True
        )
    
    def get_current_version(self, model_name: str) -> Optional[Dict[str, Any]]:
        """Get the current active version of a model"""
        if model_name not in self.metadata["current_versions"]:
            return None
        
        version_id = self.metadata["current_versions"][model_name]
        
        for version in self.metadata["models"].get(model_name, []):
            if version["version_id"] == version_id:
                return version
        
        return None
    
    def compare_versions(
        self,
        model_name: str,
        version1: str,
        version2: str
    ) -> Dict[str, Any]:
        """Compare two model versions"""
        
        versions = self.get_model_versions(model_name)
        v1_data = next((v for v in versions if v["version_id"] == version1), None)
        v2_data = next((v for v in versions if v["version_id"] == version2), None)
        
        if not v1_data or not v2_data:
            raise ValueError("One or both versions not found")
        
        comparison = {
            "version1": {
                "id": version1,
                "metrics": v1_data["metrics"],
                "created_at": v1_data["created_at"]
            },
            "version2": {
                "id": version2,
                "metrics": v2_data["metrics"],
                "created_at": v2_data["created_at"]
            },
            "metric_differences": {}
        }
        
        # Calculate metric differences
        for metric in v1_data["metrics"]:
            if metric in v2_data["metrics"]:
                diff = v2_data["metrics"][metric] - v1_data["metrics"][metric]
                comparison["metric_differences"][metric] = {
                    "difference": diff,
                    "improvement": diff > 0 if "accuracy" in metric.lower() or "precision" in metric.lower() else diff < 0
                }
        
        return comparison
    
    def rollback_model(self, model_name: str, version_id: str):
        """Rollback to a previous model version"""
        versions = self.get_model_versions(model_name)
        target_version = next((v for v in versions if v["version_id"] == version_id), None)
        
        if not target_version:
            raise ValueError(f"Version {version_id} not found for model {model_name}")
        
        # Promote the target version
        self.promote_version(model_name, version_id)
        print(f"Rolled back {model_name} to version {version_id}")
    
    def cleanup_old_versions(self, model_name: str, keep_count: int = 5):
        """Remove old model versions, keeping only the most recent ones"""
        versions = self.get_model_versions(model_name)
        
        if len(versions) <= keep_count:
            return
        
        versions_to_remove = versions[keep_count:]
        
        for version in versions_to_remove:
            if version["is_active"]:
                continue  # Don't remove active version
            
            version_dir = os.path.join(self.versions_dir, version["version_id"])
            if os.path.exists(version_dir):
                shutil.rmtree(version_dir)
            
            # Remove from metadata
            self.metadata["models"][model_name] = [
                v for v in self.metadata["models"][model_name]
                if v["version_id"] != version["version_id"]
            ]
        
        self._save_metadata()
        print(f"Cleaned up {len(versions_to_remove)} old versions for {model_name}")
    
    def export_model_info(self, model_name: str) -> Dict[str, Any]:
        """Export comprehensive model information"""
        current_version = self.get_current_version(model_name)
        all_versions = self.get_model_versions(model_name)
        
        return {
            "model_name": model_name,
            "current_version": current_version,
            "total_versions": len(all_versions),
            "version_history": all_versions,
            "performance_trend": self._calculate_performance_trend(model_name)
        }
    
    def _calculate_performance_trend(self, model_name: str) -> Dict[str, List[float]]:
        """Calculate performance trends across versions"""
        versions = self.get_model_versions(model_name)
        trends = {}
        
        for version in reversed(versions):  # Chronological order
            for metric, value in version["metrics"].items():
                if metric not in trends:
                    trends[metric] = []
                trends[metric].append(value)
        
        return trends