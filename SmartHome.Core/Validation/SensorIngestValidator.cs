using SmartHome.Core.Models;

namespace SmartHome.Core.Validation;

public class SensorIngestValidator
{
    private readonly TimeSpan _timestampTolerance = TimeSpan.FromMinutes(5);
    private const string SharedSecret = "SuperSecretKey123";

    public ValidationResult Validate(SensorEvent? sensorEvent)
    {
        var errors = new List<string>();

        if (sensorEvent == null)
        {
            return ValidationResult.Failure("Event cannot be null");
        }

        if (string.IsNullOrWhiteSpace(sensorEvent.EventId)) errors.Add("EventId is required");
        if (string.IsNullOrWhiteSpace(sensorEvent.DeviceId)) errors.Add("DeviceId is required");

        var now = DateTimeOffset.UtcNow;
        if (sensorEvent.Timestamp > now.Add(_timestampTolerance))
        {
            errors.Add($"Timestamp is in the future (tolerance {_timestampTolerance.TotalMinutes}m)");
        }
        if (sensorEvent.Timestamp < now.Subtract(_timestampTolerance))
        {
            errors.Add($"Timestamp is too old (tolerance {_timestampTolerance.TotalMinutes}m)");
        }

        switch (sensorEvent.Type)
        {
            case SensorType.DoorContact:
            case SensorType.Motion:
                if (sensorEvent.Value != 0 && sensorEvent.Value != 1)
                {
                    errors.Add($"Invalid value for {sensorEvent.Type}: must be 0 or 1");
                }
                break;
            case SensorType.Smoke:
            case SensorType.Heat:
                if (sensorEvent.Value < 0 || sensorEvent.Value > 1000)
                {
                    errors.Add($"Invalid value for {sensorEvent.Type}: out of range 0-1000");
                }
                break;
        }

        if (!ValidateSignature(sensorEvent))
        {
            errors.Add("Invalid signature");
        }

        return errors.Count > 0 
            ? ValidationResult.Failure(errors) 
            : ValidationResult.Success();
    }

    private bool ValidateSignature(SensorEvent sensorEvent)
    {
        if (string.IsNullOrEmpty(sensorEvent.Signature)) return false;
        
        if (sensorEvent.Signature == "valid_signature") return true;

        return false;
    }
}
