using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Validates save data integrity, version compatibility, and required fields
    /// </summary>
    public class SaveDataValidator
    {
        private readonly Dictionary<Type, List<ValidationRule>> _validationRules;
        private readonly Dictionary<string, int> _supportedVersions;
        
        public SaveDataValidator()
        {
            _validationRules = new Dictionary<Type, List<ValidationRule>>();
            _supportedVersions = new Dictionary<string, int>();

            InitializeDefaultRules();
        }
        
        /// <summary>
        /// Validate save data with comprehensive checks
        /// </summary>
        public ValidationResult ValidateSaveData(SaveData data)
        {
            if (data == null)
                return ValidationResult.Failed("Save data is null");
                
            var result = new ValidationResult();
            
            // Basic validation using SaveData's built-in validation
            if (!data.Validate())
            {
                result.AddError("Basic save data validation failed");
            }
            
            // Version compatibility check
            var versionResult = ValidateVersion(data);
            if (!versionResult.IsValid)
            {
                result.AddErrors(versionResult.Errors);
            }
            
            // Type-specific validation
            var typeResult = ValidateByType(data);
            if (!typeResult.IsValid)
            {
                result.AddErrors(typeResult.Errors);
            }
            
            // Custom validation rules
            var customResult = ValidateCustomRules(data);
            if (!customResult.IsValid)
            {
                result.AddErrors(customResult.Errors);
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate save metadata
        /// </summary>
        public ValidationResult ValidateMetadata(SaveMetadata metadata)
        {
            if (metadata == null)
                return ValidationResult.Failed("Save metadata is null");
                
            var result = new ValidationResult();
            
            // Required fields
            if (string.IsNullOrEmpty(metadata.SaveId))
                result.AddError("SaveId is required");
                
            if (string.IsNullOrEmpty(metadata.SaveType))
                result.AddError("SaveType is required");
                
            if (metadata.CreatedAt == default)
                result.AddError("CreatedAt is required");
                
            if (metadata.ModifiedAt == default)
                result.AddError("ModifiedAt is required");
                
            // Logical validation
            if (metadata.ModifiedAt < metadata.CreatedAt)
                result.AddError("ModifiedAt cannot be before CreatedAt");
                
            if (metadata.FileSize < 0)
                result.AddError("FileSize cannot be negative");
                
            if (metadata.UncompressedSize < 0)
                result.AddError("UncompressedSize cannot be negative");
                
            if (metadata.SaveVersion < 1)
                result.AddError("SaveVersion must be positive");
                
            if (metadata.PlayTime < 0)
                result.AddError("PlayTime cannot be negative");
                
            return result;
        }
       
        /// Check version compatibility
        /// </summary>
        public bool IsVersionSupported(SaveData data)
        {
            var saveType = data.GetType().Name;

            if (!_supportedVersions.ContainsKey(saveType))
            {
                // If no specific version support defined, use the data's current version
                var method = data.GetType().GetMethod("GetCurrentVersion",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int currentVersion = 1;
                if (method != null)
                {
                    var result = method.Invoke(data, null);
                    if (result is int intResult)
                        currentVersion = intResult;
                }
                return data.Version <= currentVersion;
            }

            return data.Version <= _supportedVersions[saveType];
        }
        
        /// <summary>
        /// Add custom validation rule
        /// </summary>
        public void AddValidationRule<T>(ValidationRule rule) where T : SaveData
        {
            var type = typeof(T);
            if (!_validationRules.ContainsKey(type))
            {
                _validationRules[type] = new List<ValidationRule>();
            }
            
            _validationRules[type].Add(rule);
        }
        
        /// <summary>
        /// Set supported version for a save data type
        /// </summary>
        public void SetSupportedVersion<T>(int version) where T : SaveData
        {
            _supportedVersions[typeof(T).Name] = version;
        }
        
        private ValidationResult ValidateVersion(SaveData data)
        {
            var result = new ValidationResult();
            
            // Check save format version
            if (string.IsNullOrEmpty(data.SaveFormatVersion))
            {
                result.AddError("SaveFormatVersion is required");
            }
            else if (!IsValidVersionFormat(data.SaveFormatVersion))
            {
                result.AddError($"Invalid SaveFormatVersion format: {data.SaveFormatVersion}");
            }
            
            // Check game version
            if (string.IsNullOrEmpty(data.GameVersion))
            {
                result.AddWarning("GameVersion is missing");
            }
            
            // Check data version compatibility
            if (!IsVersionSupported(data))
            {
                result.AddError($"Save data version {data.Version} is not supported for type {data.GetType().Name}");
            }
            
            return result;
        }
        
        private ValidationResult ValidateByType(SaveData data)
        {
            var result = new ValidationResult();
            
            switch (data)
            {
                case GameSaveData gameData:
                    ValidateGameSaveData(gameData, result);
                    break;

                case PlayerSaveData playerData:
                    ValidatePlayerSaveData(playerData, result);
                    break;

                default:
                    // Generic validation for unknown types
                    result.AddWarning($"No specific validation rules for type {data.GetType().Name}");
                    break;
            }
            
            return result;
        }
        
        private void ValidateGameSaveData(GameSaveData data, ValidationResult result)
        {
            // Validate specific GameSaveData fields
            if (data.TotalPlayTime < 0)
                result.AddError("TotalPlayTime cannot be negative");
                
            if (data.CurrentLevel < 1)
                result.AddError("CurrentLevel must be positive");
                
            if (data.GraphicsQuality < 0 || data.GraphicsQuality > 3)
                result.AddError("GraphicsQuality must be between 0 and 3");
                
            // Validate collections
            if (data.UnlockedLevels.Any(string.IsNullOrEmpty))
                result.AddError("UnlockedLevels contains invalid entries");
                
            if (data.CompletedLevels.Any(string.IsNullOrEmpty))
                result.AddError("CompletedLevels contains invalid entries");
        }

        private void ValidatePlayerSaveData(PlayerSaveData data, ValidationResult result)
        {
            // Validate specific PlayerSaveData fields
            if (data.Experience < 0)
                result.AddError("Experience cannot be negative");

            if (data.Currency < 0)
                result.AddError("Currency cannot be negative");

            if (data.InventoryCapacity <= 0)
                result.AddError("InventoryCapacity must be positive");

            if (data.Inventory.Count > data.InventoryCapacity)
                result.AddError("Inventory contains more items than capacity allows");

            // Validate inventory items
            foreach (var item in data.Inventory)
            {
                if (string.IsNullOrEmpty(item.ItemId))
                    result.AddError("Inventory contains item with invalid ItemId");

                if (item.Quantity <= 0)
                    result.AddError($"Item {item.ItemId} has invalid quantity");

                if (item.Durability < 0 || item.Durability > 100)
                    result.AddError($"Item {item.ItemId} has invalid durability");
            }

            // Validate equipped items exist in inventory
            foreach (var equippedId in data.EquippedItems)
            {
                if (!data.Inventory.Any(i => i.ItemId == equippedId))
                    result.AddWarning($"Equipped item {equippedId} not found in inventory");
            }
        }

        private ValidationResult ValidateCustomRules(SaveData data)
        {
            var result = new ValidationResult();
            var dataType = data.GetType();
            
            if (_validationRules.ContainsKey(dataType))
            {
                foreach (var rule in _validationRules[dataType])
                {
                    try
                    {
                        var ruleResult = rule.Validate(data);
                        if (!ruleResult.IsValid)
                        {
                            result.AddErrors(ruleResult.Errors);
                            result.AddWarnings(ruleResult.Warnings);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AddError($"Custom validation rule failed: {ex.Message}");
                    }
                }
            }
            
            return result;
        }
        
        private bool IsValidVersionFormat(string version)
        {
            // Check if version follows semver format (x.y.z)
            var parts = version.Split('.');
            if (parts.Length != 3)
                return false;
                
            return parts.All(part => int.TryParse(part, out _));
        }

        private void InitializeDefaultRules()
        {
            // Set default supported versions
            SetSupportedVersion<GameSaveData>(1);
            SetSupportedVersion<PlayerSaveData>(1);

            // Add default validation rules
            AddValidationRule<GameSaveData>(new ValidationRule("NonEmptySceneName",
                data => !string.IsNullOrEmpty(((GameSaveData)data).CurrentSceneName),
                "CurrentSceneName cannot be empty"));

            AddValidationRule<PlayerSaveData>(new ValidationRule("ValidPlayerId",
                data => !string.IsNullOrEmpty(((PlayerSaveData)data).PlayerId),
                "PlayerId cannot be empty"));
        }
    }
    
    /// <summary>
    /// Validation result containing errors and warnings
    /// </summary>
    public class ValidationResult
    {
        public List<string> Errors { get; private set; } = new List<string>();
        public List<string> Warnings { get; private set; } = new List<string>();
        
        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;
        
        public void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
                Errors.Add(error);
        }
        
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning))
                Warnings.Add(warning);
        }
        
        public void AddErrors(IEnumerable<string> errors)
        {
            if (errors != null)
                Errors.AddRange(errors.Where(e => !string.IsNullOrEmpty(e)));
        }
        
        public void AddWarnings(IEnumerable<string> warnings)
        {
            if (warnings != null)
                Warnings.AddRange(warnings.Where(w => !string.IsNullOrEmpty(w)));
        }
        
        public static ValidationResult Success()
        {
            return new ValidationResult();
        }
        
        public static ValidationResult Failed(string error)
        {
            var result = new ValidationResult();
            result.AddError(error);
            return result;
        }
        
        public override string ToString()
        {
            var parts = new List<string>();
            
            if (Errors.Count > 0)
                parts.Add($"Errors: {string.Join(", ", Errors)}");
                
            if (Warnings.Count > 0)
                parts.Add($"Warnings: {string.Join(", ", Warnings)}");
                
            return parts.Count > 0 ? string.Join("; ", parts) : "Valid";
        }
    }
    
    /// <summary>
    /// Custom validation rule
    /// </summary>
    public class ValidationRule
    {
        public string Name { get; }
        public Func<SaveData, bool> Validator { get; }
        public string ErrorMessage { get; }
        
        public ValidationRule(string name, Func<SaveData, bool> validator, string errorMessage)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Validator = validator ?? throw new ArgumentNullException(nameof(validator));
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        }
        
        public ValidationResult Validate(SaveData data)
        {
            var result = new ValidationResult();
            
            try
            {
                if (!Validator(data))
                {
                    result.AddError(ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Validation rule '{Name}' threw exception: {ex.Message}");
            }
            
            return result;
        }
    }
}