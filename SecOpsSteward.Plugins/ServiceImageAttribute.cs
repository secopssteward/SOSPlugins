using System;

namespace SecOpsSteward.Plugins
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceImageAttribute : Attribute
    {
        public string SVG { get; set; }

        public ServiceImageAttribute() { }
        public ServiceImageAttribute(string svg) => SVG = svg;
    }
}
