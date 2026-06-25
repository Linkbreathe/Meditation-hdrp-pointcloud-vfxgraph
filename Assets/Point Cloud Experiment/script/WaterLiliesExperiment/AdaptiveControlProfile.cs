using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public struct AdaptiveControlParameterValues
{
    public float intensity;
    public float frequency;
}

[Serializable]
public struct AdaptiveControlConditionValues
{
    public string condition_id;
    public float intensity;
    public float frequency;
}

[Serializable]
sealed class AdaptiveControlProfileDocument
{
    public string profile_id = string.Empty;
    public string schema_version = string.Empty;
    public AdaptiveControlParameterValues baseline = default(AdaptiveControlParameterValues);
    public AdaptiveControlConditionValues[] conditions = Array.Empty<AdaptiveControlConditionValues>();
}

/// <summary>
/// Reads the single adaptive-control parameter source shared with the Python runtime.
/// It intentionally does not read the Formal/Pilot ScriptableObject configuration.
/// </summary>
public sealed class AdaptiveControlProfile
{
    public const string DefaultRelativePath = "AdaptiveControl/adaptive-control-v1.json";

    readonly Dictionary<string, AdaptiveControlParameterValues> _conditions;

    AdaptiveControlProfile(
        string profileId,
        string schemaVersion,
        AdaptiveControlParameterValues baseline,
        Dictionary<string, AdaptiveControlParameterValues> conditions,
        string sha256,
        string sourcePath)
    {
        this.profileId = profileId;
        this.schemaVersion = schemaVersion;
        this.baseline = baseline;
        _conditions = conditions;
        this.sha256 = sha256;
        this.sourcePath = sourcePath;
    }

    public string profileId { get; private set; }
    public string schemaVersion { get; private set; }
    public AdaptiveControlParameterValues baseline { get; private set; }
    public string sha256 { get; private set; }
    public string sourcePath { get; private set; }

    public static string DefaultPath => Path.Combine(Application.streamingAssetsPath, DefaultRelativePath);

    public static bool TryLoad(string path, out AdaptiveControlProfile profile, out string error)
    {
        profile = null;
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "Adaptive control profile is missing: " + path;
                return false;
            }

            var raw = File.ReadAllBytes(path);
            var document = JsonUtility.FromJson<AdaptiveControlProfileDocument>(Encoding.UTF8.GetString(raw));
            if (document == null || string.IsNullOrWhiteSpace(document.profile_id))
            {
                error = "Adaptive control profile requires profile_id.";
                return false;
            }

            var conditions = new Dictionary<string, AdaptiveControlParameterValues>(StringComparer.Ordinal);
            if (document.conditions == null || document.conditions.Length != 9)
            {
                error = "Adaptive control profile must define exactly nine conditions.";
                return false;
            }

            for (var i = 0; i < document.conditions.Length; i++)
            {
                var item = document.conditions[i];
                if (!TryNormalizeCondition(item.condition_id, out var conditionId) ||
                    item.intensity < 0f || item.frequency < 0f ||
                    !IsFinite(item.intensity) || !IsFinite(item.frequency) ||
                    conditions.ContainsKey(conditionId))
                {
                    error = "Adaptive control profile contains an invalid condition entry.";
                    return false;
                }

                conditions[conditionId] = new AdaptiveControlParameterValues
                {
                    intensity = item.intensity,
                    frequency = item.frequency
                };
            }

            for (var i = 1; i <= 9; i++)
            {
                if (!conditions.ContainsKey("C" + i))
                {
                    error = "Adaptive control profile is missing C" + i + ".";
                    return false;
                }
            }

            if (document.baseline.intensity < 0f || document.baseline.frequency < 0f ||
                !IsFinite(document.baseline.intensity) || !IsFinite(document.baseline.frequency))
            {
                error = "Adaptive control profile has an invalid baseline.";
                return false;
            }

            profile = new AdaptiveControlProfile(
                document.profile_id.Trim(),
                string.IsNullOrWhiteSpace(document.schema_version) ? "1" : document.schema_version.Trim(),
                document.baseline,
                conditions,
                ComputeSha256(raw),
                path);
            return true;
        }
        catch (Exception exception)
        {
            error = "Failed to load adaptive control profile: " + exception.Message;
            return false;
        }
    }

    public bool TryGetValues(string conditionId, out AdaptiveControlParameterValues values)
    {
        values = default(AdaptiveControlParameterValues);
        return TryNormalizeCondition(conditionId, out var normalized) && _conditions.TryGetValue(normalized, out values);
    }

    public bool IsAdjacentOrSame(string currentCondition, string targetCondition)
    {
        if (!TryNormalizeCondition(currentCondition, out var current) ||
            !TryNormalizeCondition(targetCondition, out var target))
        {
            return false;
        }

        if (current == target)
        {
            return true;
        }

        var currentIndex = int.Parse(current.Substring(1)) - 1;
        var targetIndex = int.Parse(target.Substring(1)) - 1;
        var currentRow = currentIndex / 3;
        var currentColumn = currentIndex % 3;
        var targetRow = targetIndex / 3;
        var targetColumn = targetIndex % 3;
        return Mathf.Abs(currentRow - targetRow) + Mathf.Abs(currentColumn - targetColumn) == 1;
    }

    public static bool TryNormalizeCondition(string value, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length != 2 || trimmed[0] != 'C' || trimmed[1] < '1' || trimmed[1] > '9')
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static string ComputeSha256(byte[] bytes)
    {
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
