using System;
using Lumina.Core;

namespace Lumina.Signs
{
    public enum LevelKind { Numbers, Months, Greetings }

    /// <summary>Un elemento de un nivel (un número, un mes o una frase). Los textos van en ambos idiomas, como <see cref="SignEntry"/>.</summary>
    public sealed class LevelItem
    {
        public readonly string id, es, en, senaSaysEs, senaSaysEn;
        /// <summary>Id de localización del grupo (solo Saludos y frases); null si el nivel no agrupa.</summary>
        public readonly string groupKey;

        public LevelItem(string id, string es, string en, string groupKey = null, string senaSaysEs = null, string senaSaysEn = null)
        {
            this.id = id; this.es = es; this.en = en; this.groupKey = groupKey; this.senaSaysEs = senaSaysEs; this.senaSaysEn = senaSaysEn;
        }

        public string Name => Loc.Current == GameLanguage.English ? en : es;
        public string SenaSays => Loc.Current == GameLanguage.English ? senaSaysEn : senaSaysEs;
    }

    public sealed class LevelDefinition
    {
        public readonly LevelKind kind;
        public readonly string sceneName, titleKey, hintEs, hintEn;
        public readonly LevelItem[] items;

        public LevelDefinition(LevelKind kind, string sceneName, string titleKey, string hintEs, string hintEn, LevelItem[] items)
        {
            this.kind = kind; this.sceneName = sceneName; this.titleKey = titleKey; this.hintEs = hintEs; this.hintEn = hintEn; this.items = items;
        }

        private string Arg(LevelItem item, bool english) =>
            kind == LevelKind.Numbers ? item.id.Substring("numeros.".Length) : (english ? item.en : item.es).ToLowerInvariant();

        /// <summary>Descripción de la seña en el idioma activo.</summary>
        public string Hint(LevelItem item)
        {
            bool english = Loc.Current == GameLanguage.English;
            return string.Format(english ? hintEn : hintEs, Arg(item, english));
        }

        /// <summary>Entrada de práctica para este elemento: es lo que recibe la pantalla Practice (mismo contrato que el catálogo de señas).</summary>
        public SignEntry ToSignEntry(LevelItem item) => new SignEntry
        {
            id = item.id,
            displayName = item.Name,
            instruction = string.Format(hintEs, Arg(item, false)),
            instructionEn = string.Format(hintEn, Arg(item, true))
        };
    }

    /// <summary>
    /// Catálogos TEMPORALES de los niveles Números, Meses y Saludos y frases. Son datos de interfaz: cuando existan los
    /// catálogos definitivos (SignCatalog / backend) basta con reemplazar este origen; los controladores no cambian.
    /// </summary>
    public static class LearningLevels
    {
        public const string NumbersScene = "NumbersHouse", MonthsScene = "MonthsHouse", GreetingsScene = "GreetingsHouse";

        public static readonly LevelDefinition Numbers = new LevelDefinition(
            LevelKind.Numbers, NumbersScene, "title.numbers",
            "Es la seña del número {0}. Fíjate en qué dedos se usan y cómo se colocan.",
            "This is the sign for number {0}. Notice which fingers are used and how they are placed.",
            new[]
            {
                N("0", "Cero", "Zero"), N("1", "Uno", "One"), N("2", "Dos", "Two"), N("3", "Tres", "Three"), N("4", "Cuatro", "Four"),
                N("5", "Cinco", "Five"), N("6", "Seis", "Six"), N("7", "Siete", "Seven"), N("8", "Ocho", "Eight"), N("9", "Nueve", "Nine"),
                N("10", "Diez", "Ten"), N("11", "Once", "Eleven"), N("12", "Doce", "Twelve"), N("13", "Trece", "Thirteen"), N("14", "Catorce", "Fourteen"),
                N("15", "Quince", "Fifteen"), N("16", "Dieciséis", "Sixteen"), N("17", "Diecisiete", "Seventeen"), N("18", "Dieciocho", "Eighteen"), N("19", "Diecinueve", "Nineteen"),
                N("20", "Veinte", "Twenty"), N("21", "Veintiuno", "Twenty-one"), N("22", "Veintidós", "Twenty-two"), N("23", "Veintitrés", "Twenty-three"), N("24", "Veinticuatro", "Twenty-four"),
                N("25", "Veinticinco", "Twenty-five"), N("26", "Veintiséis", "Twenty-six"), N("27", "Veintisiete", "Twenty-seven"), N("28", "Veintiocho", "Twenty-eight"), N("29", "Veintinueve", "Twenty-nine"), N("30", "Treinta", "Thirty"),
            });

        public static readonly LevelDefinition Months = new LevelDefinition(
            LevelKind.Months, MonthsScene, "title.months",
            "Es la seña del mes de {0}. Observa dónde se coloca la mano y cómo se mueve.",
            "This is the sign for the month of {0}. Notice where the hand is placed and how it moves.",
            new[]
            {
                M("enero", "Enero", "January"), M("febrero", "Febrero", "February"), M("marzo", "Marzo", "March"), M("abril", "Abril", "April"),
                M("mayo", "Mayo", "May"), M("junio", "Junio", "June"), M("julio", "Julio", "July"), M("agosto", "Agosto", "August"),
                M("septiembre", "Septiembre", "September"), M("octubre", "Octubre", "October"), M("noviembre", "Noviembre", "November"),
                M("diciembre", "Diciembre", "December"),
            });

        public const string GroupGreetings = "levels.groupGreetings", GroupTalk = "levels.groupTalk";

        public static readonly LevelDefinition Greetings = new LevelDefinition(
            LevelKind.Greetings, GreetingsScene, "title.greetings",
            "Es la seña para decir «{0}». Observa con calma cómo se mueven las manos.",
            "This is the sign for saying “{0}”. Watch carefully how the hands move.",
            new[]
            {
                G("hola", "Hola", "Hello", GroupGreetings, "¡Hola, bienvenido a Lúmina!", "Hi, welcome to Lúmina!"),
                G("buenos_dias", "Buenos días", "Good morning", GroupGreetings, "¡Qué lindo amanecer!", "What a lovely sunrise!"),
                G("buenas_tardes", "Buenas tardes", "Good afternoon", GroupGreetings, "¡Qué buena tarde!", "What a nice afternoon!"),
                G("buenas_noches", "Buenas noches", "Good evening", GroupGreetings, "Ya sale la luna…", "The moon is rising…"),
                G("adios", "Adiós", "Goodbye", GroupGreetings, "¡Hasta pronto!", "See you soon!"),
                G("como_estas", "¿Cómo estás?", "How are you?", GroupTalk, "Quiero saber de ti.", "I want to know about you."),
                G("bien", "Bien", "Fine", GroupTalk, "¿Cómo estás?", "How are you?"),
                G("gracias", "Gracias", "Thank you", GroupTalk, "Toma, esto es para ti.", "Here, this is for you."),
                G("de_nada", "De nada", "You're welcome", GroupTalk, "¡Muchas gracias!", "Thank you so much!"),
                G("si", "Sí", "Yes", GroupTalk, "¿Quieres seguir aprendiendo?", "Do you want to keep learning?"),
                G("no", "No", "No", GroupTalk, "¿Quieres parar ya?", "Do you want to stop now?"),
            });

        public static LevelDefinition Get(LevelKind kind) =>
            kind == LevelKind.Numbers ? Numbers : kind == LevelKind.Months ? Months : Greetings;

        private static LevelItem N(string digit, string es, string en) => new LevelItem("numeros." + digit, es, en);
        private static LevelItem M(string id, string es, string en) => new LevelItem("meses." + id, es, en);
        private static LevelItem G(string id, string es, string en, string group, string saysEs, string saysEn) =>
            new LevelItem("saludos." + id, es, en, group, saysEs, saysEn);
    }
}
