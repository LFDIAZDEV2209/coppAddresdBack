namespace CoppAddresd.Application.Features.Nutrition.Engine;

// Menus y topes literales del prototipo validado (secciones 6 y 7).
public static class NutritionMenus
{
    public static readonly IReadOnlyDictionary<string, NutritionMenu[]> Templates = new Dictionary<string, NutritionMenu[]>
    {
        ["des"] = [
            new("Avena con frutos rojos y claras", [new("avena", 40), new("banano", 120), new("clara", 100), new("almendras", 15), new("te", 200)], "Carbohidrato complejo + proteína desde el desayuno estabiliza la glucosa."),
            new("Huevos con aguacate y pan integral", [new("huevo", 100), new("panint", 60), new("aguacate", 60), new("espinaca", 40), new("papaya", 100)], ""),
            new("Yogur griego con chía, avena y fruta", [new("yogur", 200), new("banano", 120), new("avena", 30), new("chia", 10), new("nueces", 15)], ""),
            new("Tostada con cottage, aguacate y fruta", [new("panint", 60), new("cottage", 120), new("aguacate", 40), new("ensalada", 100), new("manzana", 150)], ""),
        ],
        ["mer"] = [
            new("Manzana con almendras y yogur", [new("manzana", 180), new("almendras", 25), new("yogur", 100)], ""),
            new("Yogur con frutos rojos y nueces", [new("yogur", 150), new("frutos", 80), new("nueces", 20)], ""),
            new("Hummus con zanahoria, cottage y pan", [new("hummus", 60), new("zanahoria", 100), new("cottage", 100), new("panint", 30)], ""),
            new("Banano con nueces y yogur", [new("banano", 120), new("nueces", 20), new("yogur", 150)], ""),
        ],
        ["alm"] = [
            new("Pollo, arroz integral y ensalada", [new("pollo", 130), new("arroz", 150), new("frijol", 80), new("ensalada", 150), new("brocoli", 100), new("aceite", 10)], ""),
            new("Salmón, quinoa y brócoli", [new("salmon", 120), new("quinoa", 150), new("arroz", 100), new("brocoli", 120), new("aceite", 8)], ""),
            new("Tilapia, camote y ensalada con aguacate", [new("tilapia", 150), new("camote", 150), new("platano", 100), new("ensalada", 150), new("aguacate", 60)], ""),
            new("Lentejas con pavo y vegetales", [new("pavo", 120), new("lentejas", 200), new("arroz", 100), new("vegcoc", 150), new("aceite", 10)], ""),
        ],
        ["cen"] = [
            new("Tilapia con vegetales y camote", [new("tilapia", 150), new("vegcoc", 200), new("camote", 100), new("aceite", 10)], "Cena sin carbohidratos simples; ventana de ayuno nocturno de 12 h."),
            new("Pollo, ensalada, pan integral y aguacate", [new("pollo", 120), new("ensalada", 200), new("panint", 40), new("aguacate", 50)], ""),
            new("Atún con frijoles y espinaca", [new("atun", 100), new("arroz", 150), new("frijol", 80), new("espinaca", 80), new("ensalada", 100), new("aceite", 8)], ""),
            new("Tofu con vegetales y quinoa", [new("tofu", 150), new("vegcoc", 200), new("quinoa", 80), new("nueces", 15)], ""),
        ],
    };
    public static readonly IReadOnlyDictionary<string, (double Min, double Max)> Caps = new Dictionary<string, (double, double)>
    {
        ["avena"] = (30, 60),
        ["panint"] = (30, 90),
        ["arroz"] = (100, 220),
        ["quinoa"] = (80, 180),
        ["camote"] = (100, 200),
        ["platano"] = (80, 180),
        ["lentejas"] = (120, 250),
        ["frijol"] = (80, 200),
        ["garbanzo"] = (80, 200),
        ["frutos"] = (60, 200),
        ["manzana"] = (150, 270),
        ["banano"] = (100, 180),
        ["papaya"] = (80, 200),
        ["clara"] = (66, 200),
        ["huevo"] = (50, 150),
        ["yogur"] = (100, 250),
        ["cottage"] = (80, 200),
        ["pollo"] = (100, 180),
        ["pavo"] = (100, 180),
        ["salmon"] = (100, 160),
        ["tilapia"] = (110, 200),
        ["atun"] = (80, 140),
        ["tofu"] = (100, 200),
        ["aceite"] = (4, 14),
        ["almendras"] = (10, 30),
        ["nueces"] = (10, 25),
        ["chia"] = (5, 15),
        ["aguacate"] = (30, 80),
        ["hummus"] = (30, 80),
    };
}
