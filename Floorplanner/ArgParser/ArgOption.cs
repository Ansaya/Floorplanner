using System;
using System.Linq;

namespace Floorplanner.ArgParser
{
    public class ArgOption : IComparable<ArgOption>
    {
        public string Name { get; private set; }

        public string FullName { get; private set; }

        public string Description { get; private set; }

        public Action<string> SetValue { get; private set; }

        public bool Optional { get; private set; }

        /// <summary>
        /// Initialize a parameter option with given name and description
        /// to get the specified type back from a console parameter
        /// </summary>
        /// <param name="name">Parameter name in form 's|full' or simply 's'</param>
        /// <param name="description">Parameter description</param>
        /// <param name="valueToSet">Setter to use when the parameter is retrieved. Setter could recive null values</param>
        /// <param name="isOptional">True if this is an optional parameter</param>
        public ArgOption(string name, string description, Action<string> valueToSet, bool isOptional = false)
        {
            string[] names = name.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            Name = names[0];
            FullName = names.Length == 2 ? names[1] : String.Empty;
            Description = description;
            SetValue = valueToSet;
            Optional = isOptional;
        }

        public int CompareTo(ArgOption other)
        {
            return other.Optional && Optional ? 0 :
                other.Optional && !Optional ? -1 :
                1;
        }
    }
}
