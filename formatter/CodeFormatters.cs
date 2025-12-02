
namespace StationeersIC10Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public static class CodeFormatters
    {
        private static readonly Dictionary<string, Type> formatters = new Dictionary<string, Type>();
        private static string defaultFormatterName = "Plain";

        public static List<string> FormatterNames => new List<string>(formatters.Keys);

        public static void RegisterFormatter(string name, Type formatterType, bool isDefault = false)
        {
            L.Info($"Registering code formatter: {name}");

            if (!typeof(ICodeFormatter).IsAssignableFrom(formatterType))
                throw new ArgumentException($"Type {formatterType.FullName} does not implement ICodeFormatter.");

            formatters[name] = formatterType;

            if (isDefault)
                defaultFormatterName = name;
        }

        public static ICodeFormatter GetFormatter(string name = null)
        {
            if (name == null || !formatters.ContainsKey(name))
                return GetFormatter(defaultFormatterName);

            return CreateFormatterInstance(name, formatters[name]);
        }

        /// <summary>
        /// Finds the formatter with highest static MatchingScore(input)
        /// </summary>
        public static ICodeFormatter GetFormatterByMatching(string input)
        {
            double bestScore = double.MinValue;
            string bestName = null;

            foreach (var entry in formatters)
            {
                string name = entry.Key;
                Type type = entry.Value;

                var method = type.GetMethod("MatchingScore", BindingFlags.Public | BindingFlags.Static);

                if (method == null)
                    continue;

                double score = (double)method.Invoke(null, new object[] { input });
                L.Info($"Formatter '{name}' has matching score {score} for input.");

                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = name;
                }
            }

            return GetFormatter(bestName);
        }

        private static ICodeFormatter CreateFormatterInstance(string name, Type type)
        {
            var instance = (ICodeFormatter)Activator.CreateInstance(type);
            instance.Name = name;
            return instance;
        }
    }
}
