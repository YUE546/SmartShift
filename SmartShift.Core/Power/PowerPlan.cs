using System;

namespace SmartShift.Core.Power
{
    public class PowerPlan
    {
        public Guid Id { get; }
        public string Name { get; }
        public bool IsActive { get; }

        public PowerPlan(Guid id, string name, bool isActive)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsActive = isActive;
        }

        public override string ToString()
        {
            return $"{Name} ({Id}){(IsActive ? " [活动]" : "")}";
        }
    }
}
