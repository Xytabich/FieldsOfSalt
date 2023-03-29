# FieldsOfSalt

Mod adds an evaporation pond to the game, which makes possible to evaporate liquids to obtain resources.

### Adding recipe
It is possible to add custom pond recipes, [salt.json](https://github.com/Xytabich/FieldsOfSalt/blob/master/assets/fieldsofsalt/recipes/evaporationpond/salt.json) can be used as an example.
```
{
    "input": { ... }, // Liquid stack
    "output": { ... }, // Result stack
    "outputTexture": { "base": "..." }, // Texture used to render the content of the pond
    "evaporationTime": 24 // Evaporation time for a whole stack of liquid at 20°C
}
```
