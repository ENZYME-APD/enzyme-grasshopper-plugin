with open("Components/ThermalComfortAnalyzer.cs", "r") as f:
    text = f.read()

text = text.replace(
    '        public ThermalComfortAnalyzer()\n            : base("Thermal Comfort Analyzer", "ThermalComfort",\n                "Maps wind-engine velocity samples onto apparent (\\"feels like\\") temperature and locates the best/worst comfort points, without requiring a regular grid.",\n                "Enzyme", "Terrain")\n        {\n        }',
    '        public ThermalComfortAnalyzer()\n            : base("Thermal Comfort Analyzer", "ThermalComfort",\n                "Maps wind-engine velocity samples onto apparent (\\"feels like\\") temperature and locates the best/worst comfort points, without requiring a regular grid.",\n                "Enzyme", "Terrain")\n        {\n            this.Message = "ThermalComfort\\n-- WAITING --";\n        }'
)

with open("Components/ThermalComfortAnalyzer.cs", "w") as f:
    f.write(text)
