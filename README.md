# Fields of Salt

Mod adds an evaporation pond to the game, which makes possible to evaporate liquids to obtain resources.

Mod page: https://mods.vintagestory.at/fieldsofsalt

### Adding recipe
It is possible to add custom pond recipes, [salt.json](https://github.com/Xytabich/FieldsOfSalt/blob/master/assets/fieldsofsalt/recipes/evaporationpond/salt.json) can be used as an example.
```
{
    "input": { ... }, // Liquid stack
    "output": { ... }, // Result stack
    "outputTexture": { "base": "..." }, // Texture used to render the content of the pond
    "evaporationTime": 24 // Number of hours required for the entire stack of liquid to evaporate at 20°C
}
```

### Config
The configuration file 'fieldsofsalt.json' may contain settings:
```
{
	"maxSpeedMultiplier": 10, // Limiting the evaporation rate multiplier that ambient temperature can provide
	"baseTemperature": -30 // Temperature at which the evaporation rate drops to 0
}
```
