using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using System.Xml.Serialization;

namespace MusicXml2
{
    public static class MidiMapping
    {

        private static readonly List<Step> DifferenceArray = new List<Step> { Step.C, Step.D, Step.E, Step.F, Step.G, Step.A, Step.B };

        private static readonly Dictionary<int, string> NoteRowLookup = new Dictionary<int, string>
        {
            {0, "C"}, {1, "C#"}, {2, "D"}, {3, "D#"}, {4, "E"},{5, "F"},
            {6, "F#"}, {7, "G"}, {8, "G#"}, {9, "A"}, {10, "A#"}, {11, "B"},
        };

        private static readonly Dictionary<string, int> NoteColumnLookup = new Dictionary<string, int>
        {
            {"C", 0}, {"C#", 1}, {"D", 2}, {"D#", 3}, {"E", 4}, {"F", 5},
            {"F#", 6}, {"G", 7}, {"G#", 8}, {"A", 9}, {"A#", 10}, {"B", 11},
        };

        private static readonly int[][] MidiArray =
        {
            new [] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11},                          /*Octave 0*/
            new [] {12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23},                /*Octave 1*/
            new [] {24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35},                /*Octave 2*/
            new [] {36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47},                /*Octave 3*/
            new [] {48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59},                /*Octave 4*/
            new [] {60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71},                /*Octave 5*/
            new [] {72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83},                /*Octave 6*/
            new [] {84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95},                /*Octave 7*/
            new [] {96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107},        /*Octave 8*/
            new [] {108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119},    /*Octave 9*/
            new [] {120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131}     /*Octave 10*/
        };

        public static Interval IntervalLookUp(int baseNote, int trebleNote)
        {
            //If midiValues are equal return Unison
            //If midiValues are 12 apart return Octave

            if(baseNote == trebleNote) return new Interval(0, Type.Perfect);
            if(Math.Abs(baseNote - trebleNote) == 12) return new Interval(8, Type.Perfect);

            var bottom = StepOctaveLookUp(baseNote);
            var top = StepOctaveLookUp(trebleNote);
            var startIndex = DifferenceArray.IndexOf(bottom.Step);
            var counterIndex = startIndex;
            var span = 0;

            while (true)
            {
                span++; 
                if (DifferenceArray[++counterIndex % DifferenceArray.Count] == top.Step) break;
            }

            span++;

            Type type;

            if (IsPerfectInterval(span, baseNote, trebleNote)) type = Type.Perfect;
            else if (IsMajorInterval(span, baseNote, trebleNote)) type = Type.Major;
            else if (IsMinorInterval(span, baseNote, trebleNote)) type = Type.Minor;
            else type = Type.DiminishedAugmented;

            //Assume not key signature for now
            return new Interval(span, type);
        }

        public static Quadruplet StepOctaveLookUp(int midiNote)
        {
            var lookupNote = midiNote % 12;

            var alter = 0;

            if (lookupNote == 1 || lookupNote == 3 || lookupNote == 6 || lookupNote == 6 || lookupNote == 8 || lookupNote == 10)
            {
                alter = 1;
                lookupNote -= 1;
            }

            var octave = (midiNote / 12) - 1; 
            var step = NoteRowLookup[lookupNote];

            return new Quadruplet
                   {
                       Alteration = alter, 
                       MidiNoteValue = midiNote,
                       Step = (Step)Enum.Parse(typeof(Step), step), 
                       Octave = octave.ToString(CultureInfo.InvariantCulture)
                   };
        }

        public static int MidiLookUp(Pitch pitch)
        {

            var skip = pitch.Step;
            var alter = (int)pitch.Alter;
            var octave = Convert.ToInt32(pitch.Octave) + 1;

            if (!NoteColumnLookup.ContainsKey(skip.ToString())) throw new InvalidSkipException(skip);

            return MidiArray[octave][NoteColumnLookup[skip.ToString()]] + alter;
        }

        public struct Quadruplet
        {
            public Step Step { get; set; }
            public string Octave { get; set; }
            public int Alteration { get; set; }
            public int MidiNoteValue { get; set; }
        }

        public struct Interval
        {
            public int IntervalSpan { get; set; }
            public Type IntervalType { get; set; }
            public bool IsConsonant { get; private set; }

            internal Interval(int span, Type type) : this()
            {
                IntervalSpan = span;
                IntervalType = type;

                IsConsonant = !IsDissonant(span, type);
            }

            private static bool IsDissonant(int span, Type type)
            {
                switch (type)
                {
                    case Type.Major:
                    case Type.Minor:
                    {
                        return span == 2 || span == 7;
                    }
                    case Type.DiminishedAugmented:
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public enum Type
        {
            Perfect, Major, Minor, DiminishedAugmented
        }

        private static bool IsPerfectInterval(int interval, int note1, int note2)
        {
            //Unisoin = 0, Fourth = 5, Fifth = 7, Octave = 12,
            return (interval == 0 && ChromaticDistance(note1, note2) == 0) || 
                   (interval == 4 && ChromaticDistance(note1, note2) == 5) ||
                   (interval == 5 && ChromaticDistance(note1, note2) == 7) ||
                   (interval == 8 && ChromaticDistance(note1, note2) == 12);
        }

        private static bool IsMajorInterval(int interval, int note1, int note2)
        {
            //Second = 2, Third = 4, Six = 9, Seventh = 11
            return (interval == 2 && ChromaticDistance(note1, note2) == 2) ||
                   (interval == 3 && ChromaticDistance(note1, note2) == 4) ||
                   (interval == 6 && ChromaticDistance(note1, note2) == 9) ||
                   (interval == 7 && ChromaticDistance(note1, note2) == 11);
        }

        private static bool IsMinorInterval(int interval, int note1, int note2)
        {
            return (interval == 2 && ChromaticDistance(note1, note2) == 1) ||
                   (interval == 3 && ChromaticDistance(note1, note2) == 3) ||
                   (interval == 6 && ChromaticDistance(note1, note2) == 8) ||
                   (interval == 7 && ChromaticDistance(note1, note2) == 10);
        }

        private static int ChromaticDistance(int note1, int note2)
        {
            return Math.Abs(note1 - note2);
        }

        public static bool IsNonPerfectConsonance(Interval interval)
        {
            return interval.IntervalType != Type.DiminishedAugmented && (interval.IntervalSpan == 3 || interval.IntervalSpan == 6);
        }

        public static bool IsPerfectConsonance(Interval interval, bool includeFifth = true)
        {
            return interval.IntervalType == Type.Perfect &&
                   (interval.IntervalSpan == 0 || (interval.IntervalSpan == 5 && includeFifth) || interval.IntervalSpan == 8);
        }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    [XmlRoot("score-partwise", Namespace = "", IsNullable = false)]
    public class ScorePartwise
    {
        [XmlElement("work")]
        public Work Work { get; set; }

        [XmlElement("movement-number")]
        public string MovementNumber { get; set; }
        
        [XmlElement("movement-title")]
        public string MovementTitle { get; set; }
        
        [XmlElement("identification")]
        public Identification Identification { get; set; }
        
        [XmlElement("defautls")]
        public Defaults Defaults { get; set; }
        
        [XmlElement("credit")]
        public Credit[] Credits { get; set; }
        
        [XmlElement("part-list")]
        public PartList PartList { get; set; }
        
        [XmlElement("part")]
        public ScorePartwisePart[] Parts { get; set; }
        
        [XmlAttribute("version", DataType = "token"), DefaultValue("1.0")]
        public string Version { get; set; }

        private ScorePartwise() { }

        /// <summary>
        ///     Deserializes a Music XML file
        /// </summary>
        /// <param name="filepath">The path to the xml file</param>
        /// <returns>An instance of<see cref="ScorePartwise" /></returns>
        public static ScorePartwise Deserialize(string filepath)
        {
            if (!File.Exists(filepath)) throw new FileNotFoundException("The file " + filepath + "is invalid");

            var reader = new StringReader(File.ReadAllText(filepath));
            try
            {
                var deserializer = new XmlSerializer(typeof(ScorePartwise));
                return (ScorePartwise)deserializer.Deserialize(reader);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.InnerException.Message);
            }

            return null;
        }

        /// <summary>
        ///     Serializes a <see cref="ScorePartwise" /> to an XML file
        /// </summary>
        /// <param name="filepath">The path to the file</param>
        /// <param name="scorepartwise">the score to be serialized</param>
        public static void Serialize(string filepath, ScorePartwise scorepartwise)
        {
            if (scorepartwise == null) return;

            var serializer = new XmlSerializer(typeof(ScorePartwise));
            var xmlTextWriter = new XmlTextWriter(filepath, System.Text.Encoding.UTF8) { Formatting = Formatting.Indented };
            XmlWriter writer = XmlWriter.Create(xmlTextWriter, new XmlWriterSettings { OmitXmlDeclaration = true });

            //The oh so important DOCTYPE
            writer.WriteStartDocument();
            writer.WriteDocType("score-partwise", "-//Recordare//DTD MusicXML 2.0 Partwise//EN",
                "http://www.musicxml.org/dtds/partwise.dtd", null);

            //Create and assign an empty xml namespace to score-partwise
            var nullSpace = new XmlSerializerNamespaces();
            nullSpace.Add("", "");

            //write to file
            serializer.Serialize(writer, scorepartwise, nullSpace);
        }
    }
    
    [Serializable]
    public class Work
    {
        [XmlElement("work-number")]
        public string WorkNumber { get; set; }

        [XmlElement("work-title")]
        public string WorkTitle { get; set; }

        [XmlElement("opus")]
        public Opus Opus { get; set; }
    }

    [Serializable]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public class Opus
    {
        [XmlAttribute("href", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "anyURI")]
        public string Href { get; set; }

        [XmlAttribute("type", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink"), DefaultValue(OpusType.Simple)]
        public OpusType Type { get; set; }

        [XmlIgnore]
        public bool TypeSpecified { get; set; }

        [XmlAttribute("role", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "token")]
        public string Role { get; set; }

        [XmlAttribute("title", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "token")]
        public string Title { get; set; }

        [XmlAttribute("show", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink"), DefaultValue(OpusShow.Replace)]
        public OpusShow Show { get; set; }

        [XmlAttribute("actuate", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink"), DefaultValue(OpusActuate.OnRequest)]
        public OpusActuate Actuate { get; set; }
    }

    [Serializable]
    [XmlType("opusType", AnonymousType = true, Namespace = "http://www.w3.org/1999/xlink")]
    public enum OpusType
    {
        [XmlEnum("simple")] Simple
    }
    
    [Serializable]
    [XmlType("opusShow", AnonymousType = true, Namespace = "http://www.w3.org/1999/xlink")]
    public enum OpusShow
    {
        [XmlEnum("new")] New,
        [XmlEnum("replace")] Replace,
        [XmlEnum("embed")] Embed,
        [XmlEnum("other")] Other,
        [XmlEnum("none")] None
    }
    
    [Serializable]
    [XmlType("opusActuate", AnonymousType = true, Namespace = "http://www.w3.org/1999/xlink")]
    public enum OpusActuate
    {
        [XmlEnum("onRequest")]
        OnRequest,
        [XmlEnum("onLoad")]
        OnLoad,
        [XmlEnum("other")]
        Other,
        [XmlEnum("none")]
        None,
    }
    
    [Serializable]
    public class Feature
    {
        [XmlAttribute("type", DataType = "token")]
        public string Type { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "grouping")]
    public class Grouping
    {
        [XmlElement("feature")]
        public Feature[] Features { get; set; }

        [XmlAttribute("type")]
        public StartStopSingle Type { get; set; }

        [XmlAttribute("number", DataType = "token"), DefaultValue("1")]
        public string Number { get; set; }

        [XmlAttribute("member-of", DataType = "token")]
        public string MemberOf { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "start-stop-single")]
    public enum StartStopSingle
    {
        [XmlEnum("start")] Start,
        [XmlEnum("stop")] Stop,
        [XmlEnum("single")] Single
    }
    
    [Serializable]
    public class Repeat
    {
        [XmlAttribute("direction")]
        public BackwardForward Direction { get; set; }
        [XmlAttribute("times", DataType = "nonNegativeInteger")]
        public string Times { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "backward-forward")]
    public enum BackwardForward
    {
        [XmlEnum("backward")] Backward,
        [XmlEnum("forward")] Forward,
    }

    [Serializable]
    public class Ending
    {
        [XmlAttribute("number", DataType = "token")]
        public string Number { get; set; }

        [XmlAttribute("type")]
        public StartStopDiscontinue Type { get; set; }

        [XmlAttribute("print-object")]
        public YesNo PrintObject { get; set; }

        [XmlIgnore]
        public bool PrintObjectSpecified { get; set; }

        [XmlAttribute("end-length")]
        public decimal EndLength { get; set; }

        [XmlIgnore]
        public bool EndLengthSpecified { get; set; }

        [XmlAttribute("text-x")]
        public decimal TextX { get; set; }

        [XmlIgnore]
        public bool TextXSpecified { get; set; }

        [XmlAttribute("text-y")]
        public decimal TextY { get; set; }

        [XmlIgnore]
        public bool TextYSpecified { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "start-stop-discontinue")]
    public enum StartStopDiscontinue
    {
        [XmlEnum("start")] Start,
        [XmlEnum("stop")] Stop, 
        [XmlEnum("discontinue")] Discontinue
    }

    [Serializable]
    [XmlType(TypeName = "yes-no")]
    public enum YesNo
    {
        [XmlEnum("yes")] Yes,
        [XmlEnum("no")] No,
    }
    
    [Serializable]
    [XmlType(TypeName = "bar-style-color")]
    public class BarStyleColor
    {
        [XmlAttribute("color", DataType = "token")]
        public string Color { get; set; }

        [XmlText]
        public BarStyle Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "bar-style")]
    public enum BarStyle
    {
        [XmlEnum("regular")] Regular,
        [XmlEnum("dotted")] Dotted,
        [XmlEnum("dashed")] Dashed,
        [XmlEnum("heavy")] Heavy,
        [XmlEnum("light-light")] LightLight,
        [XmlEnum("light-heavy")] LightHeavy,
        [XmlEnum("heavy-light")] HeavyLigth,
        [XmlEnum("heavy-heavy")] HeavyHeavy,
        [XmlEnum("tick")] Tick,
        [XmlEnum("short")] Short,
        [XmlEnum("none")] None,
    }

    [Serializable]
    public class BarLine
    {
        public BarLine()
        {
            this.location = rightleftmiddle.right;
        }


        [XmlElement("bar-style")]
        public BarStyleColor barstyle { get; set; }

        public FormattedText footnote { get; set; }

        public Level level { get; set; }

        [XmlElement("wavy-line")]
        public WavyLine WavyLine { get; set; }

        public emptyprintstyle segno { get; set; }

        public emptyprintstyle coda { get; set; }

        [XmlElement("fermata")]
        public fermata[] fermata { get; set; }

        public Ending ending { get; set; }

        public Repeat repeat { get; set; }

        [XmlAttribute, DefaultValue(rightleftmiddle.right)]
        public rightleftmiddle location { get; set; }

        [XmlAttribute("segno", DataType = "token")]
        public string segno1 { get; set; }

        [XmlAttribute("coda", DataType = "token")]
        public string coda1 { get; set; }

        [XmlAttribute]
        public decimal divisions { get; set; }

        [XmlIgnore]
        public bool divisionsSpecified { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "formatted-text")]
    public class FormattedText
    {
        [XmlAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string lang { get; set; }

        [XmlAttribute]
        public Enclosure enclosure { get; set; }

        [XmlIgnore]
        public bool enclosureSpecified { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "enclosure")]
    public enum Enclosure
    {
        [XmlEnum("rectangle")] Rectangle,
        [XmlEnum("oval")] Oval,
        [XmlEnum("none")] None,
    }
    
    [Serializable]
    [XmlType(TypeName = "level")]
    public class Level
    {
        private string valueField;

        [XmlAttribute]
        public YesNo reference { get; set; }

        [XmlIgnore]
        public bool referenceSpecified { get; set; }

        [XmlAttribute]
        public YesNo parentheses { get; set; }

        [XmlIgnore]
        public bool parenthesesSpecified { get; set; }

        [XmlAttribute]
        public YesNo bracket { get; set; }

        [XmlIgnore]
        public bool bracketSpecified { get; set; }

        [XmlAttribute]
        public SymbolSize size { get; set; }

        [XmlIgnore]
        public bool sizeSpecified { get; set; }

        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "symbol-size")]
    public enum SymbolSize
    {
        [XmlEnum("full")] Full,
        [XmlEnum("cue")] Cue,
        [XmlEnum("large")] Large, 
    }
    
    [Serializable]
    [XmlType(TypeName = "wavy-line")]
    public class WavyLine
    {

        private StartStopContinue typeField;

        private string numberField;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string colorField;

        private startnote startnoteField;

        private bool startnoteFieldSpecified;

        private trillstep trillstepField;

        private bool trillstepFieldSpecified;

        private twonoteturn twonoteturnField;

        private bool twonoteturnFieldSpecified;

        private YesNo accelerateField;

        private bool accelerateFieldSpecified;

        private decimal beatsField;

        private bool beatsFieldSpecified;

        private decimal secondbeatField;

        private bool secondbeatFieldSpecified;

        private decimal lastbeatField;

        private bool lastbeatFieldSpecified;

        
        [XmlAttribute]
        public StartStopContinue type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlAttribute("start-note")]
        public startnote startnote
        {
            get
            {
                return this.startnoteField;
            }
            set
            {
                this.startnoteField = value;
            }
        }

        
        [XmlIgnore]
        public bool startnoteSpecified
        {
            get
            {
                return this.startnoteFieldSpecified;
            }
            set
            {
                this.startnoteFieldSpecified = value;
            }
        }

        
        [XmlAttribute("trill-step")]
        public trillstep trillstep
        {
            get
            {
                return this.trillstepField;
            }
            set
            {
                this.trillstepField = value;
            }
        }

        
        [XmlIgnore]
        public bool trillstepSpecified
        {
            get
            {
                return this.trillstepFieldSpecified;
            }
            set
            {
                this.trillstepFieldSpecified = value;
            }
        }

        
        [XmlAttribute("two-note-turn")]
        public twonoteturn twonoteturn
        {
            get
            {
                return this.twonoteturnField;
            }
            set
            {
                this.twonoteturnField = value;
            }
        }

        
        [XmlIgnore]
        public bool twonoteturnSpecified
        {
            get
            {
                return this.twonoteturnFieldSpecified;
            }
            set
            {
                this.twonoteturnFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo accelerate
        {
            get
            {
                return this.accelerateField;
            }
            set
            {
                this.accelerateField = value;
            }
        }

        
        [XmlIgnore]
        public bool accelerateSpecified
        {
            get
            {
                return this.accelerateFieldSpecified;
            }
            set
            {
                this.accelerateFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal beats
        {
            get
            {
                return this.beatsField;
            }
            set
            {
                this.beatsField = value;
            }
        }

        
        [XmlIgnore]
        public bool beatsSpecified
        {
            get
            {
                return this.beatsFieldSpecified;
            }
            set
            {
                this.beatsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("second-beat")]
        public decimal secondbeat
        {
            get
            {
                return this.secondbeatField;
            }
            set
            {
                this.secondbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool secondbeatSpecified
        {
            get
            {
                return this.secondbeatFieldSpecified;
            }
            set
            {
                this.secondbeatFieldSpecified = value;
            }
        }

        
        [XmlAttribute("last-beat")]
        public decimal lastbeat
        {
            get
            {
                return this.lastbeatField;
            }
            set
            {
                this.lastbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool lastbeatSpecified
        {
            get
            {
                return this.lastbeatFieldSpecified;
            }
            set
            {
                this.lastbeatFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "start-stop-continue")]
    public enum StartStopContinue
    {
        start,
        stop,
        @continue,
    }
    
    [Serializable]
    [XmlType(TypeName = "above-below")]
    public enum AboveBelow
    {

        
        above,

        
        below,
    }
    
    [Serializable]
    [XmlType(TypeName = "start-note")]
    public enum startnote
    {

        
        upper,

        
        main,

        
        below,
    }

    [Serializable]
    [XmlType(TypeName = "trill-step")]
    public enum trillstep
    {

        
        whole,

        
        half,

        
        unison,
    }
    
    [Serializable]
    [XmlType(TypeName = "two-note-turn")]
    public enum twonoteturn
    {

        
        whole,

        
        half,

        
        none,
    }
    
    [Serializable]
    [XmlType(TypeName = "empty-print-style")]
    public class emptyprintstyle
    {
    }
    
    [Serializable]
    public class fermata
    {

        private uprightinverted typeField;

        private bool typeFieldSpecified;

        private fermatashape valueField;

        
        [XmlAttribute]
        public uprightinverted type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlIgnore]
        public bool typeSpecified
        {
            get
            {
                return this.typeFieldSpecified;
            }
            set
            {
                this.typeFieldSpecified = value;
            }
        }

        
        [XmlText]
        public fermatashape Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "upright-inverted")]
    public enum uprightinverted
    {

        
        upright,

        
        inverted,
    }
    
    [Serializable]
    [XmlType(TypeName = "fermata-shape")]
    public enum fermatashape
    {

        
        normal,

        
        angled,

        
        square,

        
        [XmlEnum("")]
        Item,
    }
    
    [Serializable]
    [XmlType(TypeName = "right-left-middle")]
    public enum rightleftmiddle
    {

        
        right,

        
        left,

        
        middle,
    }
    
    [Serializable]
    [XmlType(TypeName = "measure-numbering")]
    public class measurenumbering
    {

        private measurenumberingvalue valueField;

        
        [XmlText]
        public measurenumberingvalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "measure-numbering-value")]
    public enum measurenumberingvalue
    {

        
        none,

        
        measure,

        
        system,
    }
    
    [Serializable]
    [XmlType(TypeName = "measure-layout")]
    public class measurelayout
    {

        private decimal measuredistanceField;

        private bool measuredistanceFieldSpecified;

        
        [XmlElement("measure-distance")]
        public decimal measuredistance
        {
            get
            {
                return this.measuredistanceField;
            }
            set
            {
                this.measuredistanceField = value;
            }
        }

        
        [XmlIgnore]
        public bool measuredistanceSpecified
        {
            get
            {
                return this.measuredistanceFieldSpecified;
            }
            set
            {
                this.measuredistanceFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    public class print
    {

        private pagelayout pagelayoutField;

        private systemlayout systemlayoutField;

        private stafflayout[] stafflayoutField;

        private measurelayout measurelayoutField;

        private measurenumbering measurenumberingField;

        private namedisplay partnamedisplayField;

        private namedisplay partabbreviationdisplayField;

        private decimal staffspacingField;

        private bool staffspacingFieldSpecified;

        private YesNo newsystemField;

        private bool newsystemFieldSpecified;

        private YesNo newpageField;

        private bool newpageFieldSpecified;

        private string blankpageField;

        private string pagenumberField;

        
        [XmlElement("page-layout")]
        public pagelayout pagelayout
        {
            get
            {
                return this.pagelayoutField;
            }
            set
            {
                this.pagelayoutField = value;
            }
        }

        
        [XmlElement("system-layout")]
        public systemlayout systemlayout
        {
            get
            {
                return this.systemlayoutField;
            }
            set
            {
                this.systemlayoutField = value;
            }
        }

        
        [XmlElement("staff-layout")]
        public stafflayout[] stafflayout
        {
            get
            {
                return this.stafflayoutField;
            }
            set
            {
                this.stafflayoutField = value;
            }
        }

        
        [XmlElement("measure-layout")]
        public measurelayout measurelayout
        {
            get
            {
                return this.measurelayoutField;
            }
            set
            {
                this.measurelayoutField = value;
            }
        }

        
        [XmlElement("measure-numbering")]
        public measurenumbering measurenumbering
        {
            get
            {
                return this.measurenumberingField;
            }
            set
            {
                this.measurenumberingField = value;
            }
        }

        
        [XmlElement("part-name-display")]
        public namedisplay partnamedisplay
        {
            get
            {
                return this.partnamedisplayField;
            }
            set
            {
                this.partnamedisplayField = value;
            }
        }

        
        [XmlElement("part-abbreviation-display")]
        public namedisplay partabbreviationdisplay
        {
            get
            {
                return this.partabbreviationdisplayField;
            }
            set
            {
                this.partabbreviationdisplayField = value;
            }
        }

        
        [XmlAttribute("staff-spacing")]
        public decimal staffspacing
        {
            get
            {
                return this.staffspacingField;
            }
            set
            {
                this.staffspacingField = value;
            }
        }

        
        [XmlIgnore]
        public bool staffspacingSpecified
        {
            get
            {
                return this.staffspacingFieldSpecified;
            }
            set
            {
                this.staffspacingFieldSpecified = value;
            }
        }

        
        [XmlAttribute("new-system")]
        public YesNo newsystem
        {
            get
            {
                return this.newsystemField;
            }
            set
            {
                this.newsystemField = value;
            }
        }

        
        [XmlIgnore]
        public bool newsystemSpecified
        {
            get
            {
                return this.newsystemFieldSpecified;
            }
            set
            {
                this.newsystemFieldSpecified = value;
            }
        }

        
        [XmlAttribute("new-page")]
        public YesNo newpage
        {
            get
            {
                return this.newpageField;
            }
            set
            {
                this.newpageField = value;
            }
        }

        
        [XmlIgnore]
        public bool newpageSpecified
        {
            get
            {
                return this.newpageFieldSpecified;
            }
            set
            {
                this.newpageFieldSpecified = value;
            }
        }

        
        [XmlAttribute("blank-page", DataType = "positiveInteger")]
        public string blankpage
        {
            get
            {
                return this.blankpageField;
            }
            set
            {
                this.blankpageField = value;
            }
        }

        
        [XmlAttribute("page-number", DataType = "token")]
        public string pagenumber
        {
            get
            {
                return this.pagenumberField;
            }
            set
            {
                this.pagenumberField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "page-layout")]
    public class pagelayout
    {

        private decimal pageheightField;

        private decimal pagewidthField;

        private pagemargins[] pagemarginsField;

        
        [XmlElement("page-height")]
        public decimal pageheight
        {
            get
            {
                return this.pageheightField;
            }
            set
            {
                this.pageheightField = value;
            }
        }

        
        [XmlElement("page-width")]
        public decimal pagewidth
        {
            get
            {
                return this.pagewidthField;
            }
            set
            {
                this.pagewidthField = value;
            }
        }

        
        [XmlElement("page-margins")]
        public pagemargins[] pagemargins
        {
            get
            {
                return this.pagemarginsField;
            }
            set
            {
                this.pagemarginsField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "page-margins")]
    public class pagemargins
    {

        private decimal leftmarginField;

        private decimal rightmarginField;

        private decimal topmarginField;

        private decimal bottommarginField;

        private margintype typeField;

        private bool typeFieldSpecified;

        
        [XmlElement("left-margin")]
        public decimal leftmargin
        {
            get
            {
                return this.leftmarginField;
            }
            set
            {
                this.leftmarginField = value;
            }
        }

        
        [XmlElement("right-margin")]
        public decimal rightmargin
        {
            get
            {
                return this.rightmarginField;
            }
            set
            {
                this.rightmarginField = value;
            }
        }

        
        [XmlElement("top-margin")]
        public decimal topmargin
        {
            get
            {
                return this.topmarginField;
            }
            set
            {
                this.topmarginField = value;
            }
        }

        
        [XmlElement("bottom-margin")]
        public decimal bottommargin
        {
            get
            {
                return this.bottommarginField;
            }
            set
            {
                this.bottommarginField = value;
            }
        }

        
        [XmlAttribute]
        public margintype type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlIgnore]
        public bool typeSpecified
        {
            get
            {
                return this.typeFieldSpecified;
            }
            set
            {
                this.typeFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "margin-type")]
    public enum margintype
    {

        
        odd,

        
        even,

        
        both,
    }
    
    [Serializable]
    [XmlType(TypeName = "system-layout")]
    public class systemlayout
    {

        private systemmargins systemmarginsField;

        private decimal systemdistanceField;

        private bool systemdistanceFieldSpecified;

        private decimal topsystemdistanceField;

        private bool topsystemdistanceFieldSpecified;

        
        [XmlElement("system-margins")]
        public systemmargins systemmargins
        {
            get
            {
                return this.systemmarginsField;
            }
            set
            {
                this.systemmarginsField = value;
            }
        }

        
        [XmlElement("system-distance")]
        public decimal systemdistance
        {
            get
            {
                return this.systemdistanceField;
            }
            set
            {
                this.systemdistanceField = value;
            }
        }

        
        [XmlIgnore]
        public bool systemdistanceSpecified
        {
            get
            {
                return this.systemdistanceFieldSpecified;
            }
            set
            {
                this.systemdistanceFieldSpecified = value;
            }
        }

        
        [XmlElement("top-system-distance")]
        public decimal topsystemdistance
        {
            get
            {
                return this.topsystemdistanceField;
            }
            set
            {
                this.topsystemdistanceField = value;
            }
        }

        
        [XmlIgnore]
        public bool topsystemdistanceSpecified
        {
            get
            {
                return this.topsystemdistanceFieldSpecified;
            }
            set
            {
                this.topsystemdistanceFieldSpecified = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "system-margins")]
    public class systemmargins
    {

        private decimal leftmarginField;

        private decimal rightmarginField;

        
        [XmlElement("left-margin")]
        public decimal leftmargin
        {
            get
            {
                return this.leftmarginField;
            }
            set
            {
                this.leftmarginField = value;
            }
        }

        
        [XmlElement("right-margin")]
        public decimal rightmargin
        {
            get
            {
                return this.rightmarginField;
            }
            set
            {
                this.rightmarginField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "staff-layout")]
    public class stafflayout
    {

        private decimal staffdistanceField;

        private bool staffdistanceFieldSpecified;

        private string numberField;

        
        [XmlElement("staff-distance")]
        public decimal staffdistance
        {
            get
            {
                return this.staffdistanceField;
            }
            set
            {
                this.staffdistanceField = value;
            }
        }

        
        [XmlIgnore]
        public bool staffdistanceSpecified
        {
            get
            {
                return this.staffdistanceFieldSpecified;
            }
            set
            {
                this.staffdistanceFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "name-display")]
    public class namedisplay
    {

        private object[] itemsField;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        
        [XmlElement("accidental-text", typeof(accidentaltext))]
        [XmlElement("display-text", typeof(FormattedText))]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "accidental-text")]
    public class accidentaltext
    {

        private string langField;

        private Enclosure enclosureField;

        private bool enclosureFieldSpecified;

        private accidentalvalue valueField;

        
        [XmlAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string lang
        {
            get
            {
                return this.langField;
            }
            set
            {
                this.langField = value;
            }
        }

        
        [XmlAttribute]
        public Enclosure enclosure
        {
            get
            {
                return this.enclosureField;
            }
            set
            {
                this.enclosureField = value;
            }
        }

        
        [XmlIgnore]
        public bool enclosureSpecified
        {
            get
            {
                return this.enclosureFieldSpecified;
            }
            set
            {
                this.enclosureFieldSpecified = value;
            }
        }

        
        [XmlText]
        public accidentalvalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "accidental-value")]
    public enum accidentalvalue
    {

        
        sharp,

        
        natural,

        
        flat,

        
        [XmlEnum("double-sharp")]
        doublesharp,

        
        [XmlEnum("sharp-sharp")]
        sharpsharp,

        
        [XmlEnum("flat-flat")]
        flatflat,

        
        [XmlEnum("natural-sharp")]
        naturalsharp,

        
        [XmlEnum("natural-flat")]
        naturalflat,

        
        [XmlEnum("quarter-flat")]
        quarterflat,

        
        [XmlEnum("quarter-sharp")]
        quartersharp,

        
        [XmlEnum("three-quarters-flat")]
        threequartersflat,

        
        [XmlEnum("three-quarters-sharp")]
        threequarterssharp,
    }
    
    [Serializable]
    public class figure
    {

        private styletext prefixField;

        private styletext figurenumberField;

        private styletext suffixField;

        private extend extendField;

        
        public styletext prefix
        {
            get
            {
                return this.prefixField;
            }
            set
            {
                this.prefixField = value;
            }
        }

        
        [XmlElement("figure-number")]
        public styletext figurenumber
        {
            get
            {
                return this.figurenumberField;
            }
            set
            {
                this.figurenumberField = value;
            }
        }

        
        public styletext suffix
        {
            get
            {
                return this.suffixField;
            }
            set
            {
                this.suffixField = value;
            }
        }

        
        public extend extend
        {
            get
            {
                return this.extendField;
            }
            set
            {
                this.extendField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "style-text")]
    public class styletext
    {

        private string valueField;

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    public class extend
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "font-style")]
    public enum FontStyle
    {
        [XmlEnum("normal")] Normal,
        [XmlEnum("italic")] Italic,
    }

    [Serializable]
    [XmlType(TypeName = "font-weight")]
    public enum FontWeight
    {
        [XmlEnum("normal")] Normal,
        [XmlEnum("bold")] Bold
    }
    
    [Serializable]
    [XmlType(TypeName = "figured-bass")]
    public class FiguredBass
    {
        [XmlElement("figure")]
        public figure[] Figure { get; set; }

        [XmlElement("duration")]
        public decimal Duration { get; set; }

        [XmlElement("footnote")]
        public FormattedText Footnote { get; set; }

        [XmlElement("level")]
        public Level Level { get; set; }

        [XmlAttribute("print-dot")]
        public YesNo PrintDot { get; set; }

        [XmlIgnore]
        public bool PrintDotSpecified { get; set; }

        [XmlAttribute("print-lyric")]
        public YesNo PrintLyric { get; set; }

        [XmlIgnore]
        public bool PrintLyricSpecified { get; set; }

        [XmlAttribute("parentheses")]
        public YesNo Parentheses { get; set; }

        [XmlIgnore]
        public bool ParenthesesSpecified { get; set; }
    }
    
    [Serializable]
    public class barre
    {

        private startstop typeField;

        private string colorField;

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "start-stop")]
    public enum startstop
    {

        
        start,

        
        stop,
    }
    
    [Serializable]
    [XmlType(TypeName = "frame-note")]
    public class framenote
    {

        private String stringField;

        private fret fretField;

        private fingering fingeringField;

        private barre barreField;

        
        public String @string
        {
            get
            {
                return this.stringField;
            }
            set
            {
                this.stringField = value;
            }
        }

        
        public fret fret
        {
            get
            {
                return this.fretField;
            }
            set
            {
                this.fretField = value;
            }
        }

        
        public fingering fingering
        {
            get
            {
                return this.fingeringField;
            }
            set
            {
                this.fingeringField = value;
            }
        }

        
        public barre barre
        {
            get
            {
                return this.barreField;
            }
            set
            {
                this.barreField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "string")]
    public class String
    {

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string valueField;

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlTextAttribute(DataType = "positiveInteger")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    public class fret
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        private string valueField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlTextAttribute(DataType = "nonNegativeInteger")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    public class fingering
    {

        private YesNo substitutionField;

        private bool substitutionFieldSpecified;

        private YesNo alternateField;

        private bool alternateFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string valueField;

        
        [XmlAttribute]
        public YesNo substitution
        {
            get
            {
                return this.substitutionField;
            }
            set
            {
                this.substitutionField = value;
            }
        }

        
        [XmlIgnore]
        public bool substitutionSpecified
        {
            get
            {
                return this.substitutionFieldSpecified;
            }
            set
            {
                this.substitutionFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo alternate
        {
            get
            {
                return this.alternateField;
            }
            set
            {
                this.alternateField = value;
            }
        }

        
        [XmlIgnore]
        public bool alternateSpecified
        {
            get
            {
                return this.alternateFieldSpecified;
            }
            set
            {
                this.alternateFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "first-fret")]
    public class firstfret
    {

        private string textField;

        private leftright locationField;

        private bool locationFieldSpecified;

        private string valueField;

        
        [XmlAttribute(DataType = "token")]
        public string text
        {
            get
            {
                return this.textField;
            }
            set
            {
                this.textField = value;
            }
        }

        
        [XmlAttribute]
        public leftright location
        {
            get
            {
                return this.locationField;
            }
            set
            {
                this.locationField = value;
            }
        }

        
        [XmlIgnore]
        public bool locationSpecified
        {
            get
            {
                return this.locationFieldSpecified;
            }
            set
            {
                this.locationFieldSpecified = value;
            }
        }

        
        [XmlTextAttribute(DataType = "positiveInteger")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "left-right")]
    public enum leftright
    {

        
        left,

        
        right,
    }

    
    
    [Serializable]
    
    
    public class frame
    {

        private string framestringsField;

        private string framefretsField;

        private firstfret firstfretField;

        private framenote[] framenoteField;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private string colorField;

        private leftcenterright halignField;

        private bool halignFieldSpecified;

        private valign valignField;

        private bool valignFieldSpecified;

        private decimal heightField;

        private bool heightFieldSpecified;

        private decimal widthField;

        private bool widthFieldSpecified;

        
        [XmlElement("frame-strings", DataType = "positiveInteger")]
        public string framestrings
        {
            get
            {
                return this.framestringsField;
            }
            set
            {
                this.framestringsField = value;
            }
        }

        
        [XmlElement("frame-frets", DataType = "positiveInteger")]
        public string framefrets
        {
            get
            {
                return this.framefretsField;
            }
            set
            {
                this.framefretsField = value;
            }
        }

        
        [XmlElement("first-fret")]
        public firstfret firstfret
        {
            get
            {
                return this.firstfretField;
            }
            set
            {
                this.firstfretField = value;
            }
        }

        
        [XmlElement("frame-note")]
        public framenote[] framenote
        {
            get
            {
                return this.framenoteField;
            }
            set
            {
                this.framenoteField = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlAttribute]
        public leftcenterright halign
        {
            get
            {
                return this.halignField;
            }
            set
            {
                this.halignField = value;
            }
        }

        
        [XmlIgnore]
        public bool halignSpecified
        {
            get
            {
                return this.halignFieldSpecified;
            }
            set
            {
                this.halignFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public valign valign
        {
            get
            {
                return this.valignField;
            }
            set
            {
                this.valignField = value;
            }
        }

        
        [XmlIgnore]
        public bool valignSpecified
        {
            get
            {
                return this.valignFieldSpecified;
            }
            set
            {
                this.valignFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal height
        {
            get
            {
                return this.heightField;
            }
            set
            {
                this.heightField = value;
            }
        }

        
        [XmlIgnore]
        public bool heightSpecified
        {
            get
            {
                return this.heightFieldSpecified;
            }
            set
            {
                this.heightFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal width
        {
            get
            {
                return this.widthField;
            }
            set
            {
                this.widthField = value;
            }
        }

        
        [XmlIgnore]
        public bool widthSpecified
        {
            get
            {
                return this.widthFieldSpecified;
            }
            set
            {
                this.widthFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "left-center-right")]
    public enum leftcenterright
    {

        
        left,

        
        center,

        
        right,
    }

    
    
    [Serializable]
    public enum valign
    {

        
        top,

        
        middle,

        
        bottom,

        
        baseline,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "degree-type")]
    public class degreetype
    {

        private string textField;

        private degreetypevalue valueField;

        
        [XmlAttribute(DataType = "token")]
        public string text
        {
            get
            {
                return this.textField;
            }
            set
            {
                this.textField = value;
            }
        }

        
        [XmlText]
        public degreetypevalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "degree-type-value")]
    public enum degreetypevalue
    {

        
        add,

        
        alter,

        
        subtract,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "degree-alter")]
    public class degreealter
    {

        private YesNo plusminusField;

        private bool plusminusFieldSpecified;

        private decimal valueField;

        
        [XmlAttribute("plus-minus")]
        public YesNo plusminus
        {
            get
            {
                return this.plusminusField;
            }
            set
            {
                this.plusminusField = value;
            }
        }

        
        [XmlIgnore]
        public bool plusminusSpecified
        {
            get
            {
                return this.plusminusFieldSpecified;
            }
            set
            {
                this.plusminusFieldSpecified = value;
            }
        }

        
        [XmlText]
        public decimal Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "degree-value")]
    public class degreevalue
    {

        private string textField;

        private string valueField;

        
        [XmlAttribute(DataType = "token")]
        public string text
        {
            get
            {
                return this.textField;
            }
            set
            {
                this.textField = value;
            }
        }

        
        [XmlTextAttribute(DataType = "positiveInteger")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class degree
    {

        private degreevalue degreevalueField;

        private degreealter degreealterField;

        private degreetype degreetypeField;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        
        [XmlElement("degree-value")]
        public degreevalue degreevalue
        {
            get
            {
                return this.degreevalueField;
            }
            set
            {
                this.degreevalueField = value;
            }
        }

        
        [XmlElement("degree-alter")]
        public degreealter degreealter
        {
            get
            {
                return this.degreealterField;
            }
            set
            {
                this.degreealterField = value;
            }
        }

        
        [XmlElement("degree-type")]
        public degreetype degreetype
        {
            get
            {
                return this.degreetypeField;
            }
            set
            {
                this.degreetypeField = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "bass-alter")]
    public class bassalter
    {

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        private leftright locationField;

        private bool locationFieldSpecified;

        private decimal valueField;

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public leftright location
        {
            get
            {
                return this.locationField;
            }
            set
            {
                this.locationField = value;
            }
        }

        
        [XmlIgnore]
        public bool locationSpecified
        {
            get
            {
                return this.locationFieldSpecified;
            }
            set
            {
                this.locationFieldSpecified = value;
            }
        }

        
        [XmlText]
        public decimal Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "bass-step")]
    public class bassstep
    {

        private string textField;

        private Step valueField;

        
        [XmlAttribute(DataType = "token")]
        public string text
        {
            get
            {
                return this.textField;
            }
            set
            {
                this.textField = value;
            }
        }

        
        [XmlText]
        public Step Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    public enum Step
    {
        A, B, C, D, E, F, G,
    }
    
    [Serializable]
    public class bass
    {

        private bassstep bassstepField;

        private bassalter bassalterField;

        
        [XmlElement("bass-step")]
        public bassstep bassstep
        {
            get
            {
                return this.bassstepField;
            }
            set
            {
                this.bassstepField = value;
            }
        }

        
        [XmlElement("bass-alter")]
        public bassalter bassalter
        {
            get
            {
                return this.bassalterField;
            }
            set
            {
                this.bassalterField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class inversion
    {

        private string valueField;

        
        [XmlTextAttribute(DataType = "nonNegativeInteger")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class kind
    {

        private YesNo usesymbolsField;

        private bool usesymbolsFieldSpecified;

        private string textField;

        private YesNo stackdegreesField;

        private bool stackdegreesFieldSpecified;

        private YesNo parenthesesdegreesField;

        private bool parenthesesdegreesFieldSpecified;

        private YesNo bracketdegreesField;

        private bool bracketdegreesFieldSpecified;

        private leftcenterright halignField;

        private bool halignFieldSpecified;

        private valign valignField;

        private bool valignFieldSpecified;

        private kindvalue valueField;

        
        [XmlAttribute("use-symbols")]
        public YesNo usesymbols
        {
            get
            {
                return this.usesymbolsField;
            }
            set
            {
                this.usesymbolsField = value;
            }
        }

        
        [XmlIgnore]
        public bool usesymbolsSpecified
        {
            get
            {
                return this.usesymbolsFieldSpecified;
            }
            set
            {
                this.usesymbolsFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string text
        {
            get
            {
                return this.textField;
            }
            set
            {
                this.textField = value;
            }
        }

        
        [XmlAttribute("stack-degrees")]
        public YesNo stackdegrees
        {
            get
            {
                return this.stackdegreesField;
            }
            set
            {
                this.stackdegreesField = value;
            }
        }

        
        [XmlIgnore]
        public bool stackdegreesSpecified
        {
            get
            {
                return this.stackdegreesFieldSpecified;
            }
            set
            {
                this.stackdegreesFieldSpecified = value;
            }
        }

        
        [XmlAttribute("parentheses-degrees")]
        public YesNo parenthesesdegrees
        {
            get
            {
                return this.parenthesesdegreesField;
            }
            set
            {
                this.parenthesesdegreesField = value;
            }
        }

        
        [XmlIgnore]
        public bool parenthesesdegreesSpecified
        {
            get
            {
                return this.parenthesesdegreesFieldSpecified;
            }
            set
            {
                this.parenthesesdegreesFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bracket-degrees")]
        public YesNo bracketdegrees
        {
            get
            {
                return this.bracketdegreesField;
            }
            set
            {
                this.bracketdegreesField = value;
            }
        }

        
        [XmlIgnore]
        public bool bracketdegreesSpecified
        {
            get
            {
                return this.bracketdegreesFieldSpecified;
            }
            set
            {
                this.bracketdegreesFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public leftcenterright halign
        {
            get
            {
                return this.halignField;
            }
            set
            {
                this.halignField = value;
            }
        }

        
        [XmlIgnore]
        public bool halignSpecified
        {
            get
            {
                return this.halignFieldSpecified;
            }
            set
            {
                this.halignFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public valign valign
        {
            get
            {
                return this.valignField;
            }
            set
            {
                this.valignField = value;
            }
        }

        
        [XmlIgnore]
        public bool valignSpecified
        {
            get
            {
                return this.valignFieldSpecified;
            }
            set
            {
                this.valignFieldSpecified = value;
            }
        }

        
        [XmlText]
        public kindvalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "kind-value")]
    public enum kindvalue
    {

        
        major,

        
        minor,

        
        augmented,

        
        diminished,

        
        dominant,

        
        [XmlEnum("major-seventh")]
        majorseventh,

        
        [XmlEnum("minor-seventh")]
        minorseventh,

        
        [XmlEnum("diminished-seventh")]
        diminishedseventh,

        
        [XmlEnum("augmented-seventh")]
        augmentedseventh,

        
        [XmlEnum("half-diminished")]
        halfdiminished,

        
        [XmlEnum("major-minor")]
        majorminor,

        
        [XmlEnum("major-sixth")]
        majorsixth,

        
        [XmlEnum("minor-sixth")]
        minorsixth,

        
        [XmlEnum("dominant-ninth")]
        dominantninth,

        
        [XmlEnum("major-ninth")]
        majorninth,

        
        [XmlEnum("minor-ninth")]
        minorninth,

        
        [XmlEnum("dominant-11th")]
        dominant11th,

        
        [XmlEnum("major-11th")]
        major11th,

        
        [XmlEnum("minor-11th")]
        minor11th,

        
        [XmlEnum("dominant-13th")]
        dominant13th,

        
        [XmlEnum("major-13th")]
        major13th,

        
        [XmlEnum("minor-13th")]
        minor13th,

        
        [XmlEnum("suspended-second")]
        suspendedsecond,

        
        [XmlEnum("suspended-fourth")]
        suspendedfourth,

        
        Neapolitan,

        
        Italian,

        
        French,

        
        German,

        
        pedal,

        
        power,

        
        Tristan,

        
        other,

        
        none,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "root-alter")]
    public class rootalter
    {

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        private leftright locationField;

        private bool locationFieldSpecified;

        private decimal valueField;

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public leftright location
        {
            get
            {
                return this.locationField;
            }
            set
            {
                this.locationField = value;
            }
        }

        
        [XmlIgnore]
        public bool locationSpecified
        {
            get
            {
                return this.locationFieldSpecified;
            }
            set
            {
                this.locationFieldSpecified = value;
            }
        }

        
        [XmlText]
        public decimal Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "root-step")]
    public class rootstep
    {

        private string textField;

        private Step valueField;

        
        [XmlAttribute(DataType = "token")]
        public string text
        {
            get
            {
                return this.textField;
            }
            set
            {
                this.textField = value;
            }
        }

        
        [XmlText]
        public Step Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class root
    {

        private rootstep rootstepField;

        private rootalter rootalterField;

        
        [XmlElement("root-step")]
        public rootstep rootstep
        {
            get
            {
                return this.rootstepField;
            }
            set
            {
                this.rootstepField = value;
            }
        }

        
        [XmlElement("root-alter")]
        public rootalter rootalter
        {
            get
            {
                return this.rootalterField;
            }
            set
            {
                this.rootalterField = value;
            }
        }
    }

    
    
    [Serializable]
    public class harmony
    {

        private object[] itemsField;

        private kind[] kindField;

        private inversion[] inversionField;

        private bass[] bassField;

        private degree[] degreeField;

        private frame frameField;

        private Offset offsetField;

        private FormattedText footnoteField;

        private Level levelField;

        private string staffField;

        private harmonytype typeField;

        private bool typeFieldSpecified;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        private YesNo printframeField;

        private bool printframeFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        
        [XmlElement("function", typeof(styletext))]
        [XmlElement("root", typeof(root))]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("kind")]
        public kind[] kind
        {
            get
            {
                return this.kindField;
            }
            set
            {
                this.kindField = value;
            }
        }

        
        [XmlElement("inversion")]
        public inversion[] inversion
        {
            get
            {
                return this.inversionField;
            }
            set
            {
                this.inversionField = value;
            }
        }

        
        [XmlElement("bass")]
        public bass[] bass
        {
            get
            {
                return this.bassField;
            }
            set
            {
                this.bassField = value;
            }
        }

        
        [XmlElement("degree")]
        public degree[] degree
        {
            get
            {
                return this.degreeField;
            }
            set
            {
                this.degreeField = value;
            }
        }

        
        public frame frame
        {
            get
            {
                return this.frameField;
            }
            set
            {
                this.frameField = value;
            }
        }

        
        public Offset offset
        {
            get
            {
                return this.offsetField;
            }
            set
            {
                this.offsetField = value;
            }
        }

        
        public FormattedText footnote
        {
            get
            {
                return this.footnoteField;
            }
            set
            {
                this.footnoteField = value;
            }
        }

        
        public Level level
        {
            get
            {
                return this.levelField;
            }
            set
            {
                this.levelField = value;
            }
        }

        
        [XmlElement(DataType = "positiveInteger")]
        public string staff
        {
            get
            {
                return this.staffField;
            }
            set
            {
                this.staffField = value;
            }
        }

        
        [XmlAttribute]
        public harmonytype type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlIgnore]
        public bool typeSpecified
        {
            get
            {
                return this.typeFieldSpecified;
            }
            set
            {
                this.typeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }

        
        [XmlAttribute("print-frame")]
        public YesNo printframe
        {
            get
            {
                return this.printframeField;
            }
            set
            {
                this.printframeField = value;
            }
        }

        
        [XmlIgnore]
        public bool printframeSpecified
        {
            get
            {
                return this.printframeFieldSpecified;
            }
            set
            {
                this.printframeFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    public class Offset
    {

        private YesNo soundField;

        private bool soundFieldSpecified;

        private decimal valueField;

        
        [XmlAttribute]
        public YesNo sound
        {
            get
            {
                return this.soundField;
            }
            set
            {
                this.soundField = value;
            }
        }

        
        [XmlIgnore]
        public bool soundSpecified
        {
            get
            {
                return this.soundFieldSpecified;
            }
            set
            {
                this.soundFieldSpecified = value;
            }
        }

        
        [XmlText]
        public decimal Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "harmony-type")]
    public enum harmonytype
    {

        
        @explicit,

        
        implied,

        
        alternate,
    }
    
    [Serializable]
    public class slash
    {

        private NoteTypeValue slashtypeField;

        private empty[] slashdotField;

        private startstop typeField;

        private YesNo usedotsField;

        private bool usedotsFieldSpecified;

        private YesNo usestemsField;

        private bool usestemsFieldSpecified;

        
        [XmlElement("slash-type")]
        public NoteTypeValue slashtype
        {
            get
            {
                return this.slashtypeField;
            }
            set
            {
                this.slashtypeField = value;
            }
        }

        
        [XmlElement("slash-dot")]
        public empty[] slashdot
        {
            get
            {
                return this.slashdotField;
            }
            set
            {
                this.slashdotField = value;
            }
        }

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute("use-dots")]
        public YesNo usedots
        {
            get
            {
                return this.usedotsField;
            }
            set
            {
                this.usedotsField = value;
            }
        }

        
        [XmlIgnore]
        public bool usedotsSpecified
        {
            get
            {
                return this.usedotsFieldSpecified;
            }
            set
            {
                this.usedotsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("use-stems")]
        public YesNo usestems
        {
            get
            {
                return this.usestemsField;
            }
            set
            {
                this.usestemsField = value;
            }
        }

        
        [XmlIgnore]
        public bool usestemsSpecified
        {
            get
            {
                return this.usestemsFieldSpecified;
            }
            set
            {
                this.usestemsFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "note-type-value")]
    public enum NoteTypeValue
    {
        [XmlEnum("256th")] A256Th,
        [XmlEnum("128th")] A128Th,
        [XmlEnum("64th")] HemiDemiSemiQuaver,
        [XmlEnum("32nd")] DemiSemiQuaver,
        [XmlEnum("16th")] SemiQuaver,
        [XmlEnum("eight")] Quaver,
        [XmlEnum("quarter")] Chrotchet,
        [XmlEnum("half")] Minim,
        [XmlEnum("whole")] SemiBreve,
        [XmlEnum("breve")] Breve, 
        [XmlEnum("long")] DoubleBreve
    }

    [Serializable]
    public class empty
    {
    }
    
    [Serializable]
    [XmlType(TypeName = "beat-repeat")]
    public class beatrepeat
    {
        [XmlElement("slash-type")]
        public NoteTypeValue SlashType { get; set; }


        [XmlElement("slash-dot")]
        public empty[] slashdot { get; set; }


        [XmlAttribute]
        public startstop type { get; set; }


        [XmlAttribute(DataType = "positiveInteger")]
        public string slashes { get; set; }


        [XmlAttribute("use-dots")]
        public YesNo usedots { get; set; }


        [XmlIgnore]
        public bool usedotsSpecified { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "measure-repeat")]
    public class measurerepeat
    {

        private startstop typeField;

        private string slashesField;

        private string valueField;

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string slashes
        {
            get
            {
                return this.slashesField;
            }
            set
            {
                this.slashesField = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "multiple-rest")]
    public class multiplerest
    {

        private YesNo usesymbolsField;

        private bool usesymbolsFieldSpecified;

        private string valueField;

        
        [XmlAttribute("use-symbols")]
        public YesNo usesymbols
        {
            get
            {
                return this.usesymbolsField;
            }
            set
            {
                this.usesymbolsField = value;
            }
        }

        
        [XmlIgnore]
        public bool usesymbolsSpecified
        {
            get
            {
                return this.usesymbolsFieldSpecified;
            }
            set
            {
                this.usesymbolsFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "measure-style")]
    public class measurestyle
    {

        private object itemField;

        private string numberField;

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        
        [XmlElement("beat-repeat", typeof(beatrepeat))]
        [XmlElement("measure-repeat", typeof(measurerepeat))]
        [XmlElement("multiple-rest", typeof(multiplerest))]
        [XmlElement("slash", typeof(slash))]
        public object Item
        {
            get
            {
                return this.itemField;
            }
            set
            {
                this.itemField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class transpose
    {

        private string diatonicField;

        private decimal chromaticField;

        private string octavechangeField;

        private empty doubleField;

        
        [XmlElement(DataType = "integer")]
        public string diatonic
        {
            get
            {
                return this.diatonicField;
            }
            set
            {
                this.diatonicField = value;
            }
        }

        
        public decimal chromatic
        {
            get
            {
                return this.chromaticField;
            }
            set
            {
                this.chromaticField = value;
            }
        }

        
        [XmlElement("octave-change", DataType = "integer")]
        public string octavechange
        {
            get
            {
                return this.octavechangeField;
            }
            set
            {
                this.octavechangeField = value;
            }
        }

        
        public empty @double
        {
            get
            {
                return this.doubleField;
            }
            set
            {
                this.doubleField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "staff-tuning")]
    public class stafftuning
    {

        private Step tuningstepField;

        private decimal tuningalterField;

        private bool tuningalterFieldSpecified;

        private string tuningoctaveField;

        private string lineField;

        
        [XmlElement("tuning-step")]
        public Step tuningstep
        {
            get
            {
                return this.tuningstepField;
            }
            set
            {
                this.tuningstepField = value;
            }
        }

        
        [XmlElement("tuning-alter")]
        public decimal tuningalter
        {
            get
            {
                return this.tuningalterField;
            }
            set
            {
                this.tuningalterField = value;
            }
        }

        
        [XmlIgnore]
        public bool tuningalterSpecified
        {
            get
            {
                return this.tuningalterFieldSpecified;
            }
            set
            {
                this.tuningalterFieldSpecified = value;
            }
        }

        
        [XmlElement("tuning-octave", DataType = "integer")]
        public string tuningoctave
        {
            get
            {
                return this.tuningoctaveField;
            }
            set
            {
                this.tuningoctaveField = value;
            }
        }

        
        [XmlAttribute(DataType = "integer")]
        public string line
        {
            get
            {
                return this.lineField;
            }
            set
            {
                this.lineField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "staff-details")]
    public class staffdetails
    {

        private stafftype stafftypeField;

        private bool stafftypeFieldSpecified;

        private string stafflinesField;

        private stafftuning[] stafftuningField;

        private string capoField;

        private decimal staffsizeField;

        private bool staffsizeFieldSpecified;

        private string numberField;

        private showfrets showfretsField;

        private bool showfretsFieldSpecified;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        private YesNo printspacingField;

        private bool printspacingFieldSpecified;

        
        [XmlElement("staff-type")]
        public stafftype stafftype
        {
            get
            {
                return this.stafftypeField;
            }
            set
            {
                this.stafftypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool stafftypeSpecified
        {
            get
            {
                return this.stafftypeFieldSpecified;
            }
            set
            {
                this.stafftypeFieldSpecified = value;
            }
        }

        
        [XmlElement("staff-lines", DataType = "nonNegativeInteger")]
        public string stafflines
        {
            get
            {
                return this.stafflinesField;
            }
            set
            {
                this.stafflinesField = value;
            }
        }

        
        [XmlElement("staff-tuning")]
        public stafftuning[] stafftuning
        {
            get
            {
                return this.stafftuningField;
            }
            set
            {
                this.stafftuningField = value;
            }
        }

        
        [XmlElement(DataType = "nonNegativeInteger")]
        public string capo
        {
            get
            {
                return this.capoField;
            }
            set
            {
                this.capoField = value;
            }
        }

        
        [XmlElement("staff-size")]
        public decimal staffsize
        {
            get
            {
                return this.staffsizeField;
            }
            set
            {
                this.staffsizeField = value;
            }
        }

        
        [XmlIgnore]
        public bool staffsizeSpecified
        {
            get
            {
                return this.staffsizeFieldSpecified;
            }
            set
            {
                this.staffsizeFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("show-frets")]
        public showfrets showfrets
        {
            get
            {
                return this.showfretsField;
            }
            set
            {
                this.showfretsField = value;
            }
        }

        
        [XmlIgnore]
        public bool showfretsSpecified
        {
            get
            {
                return this.showfretsFieldSpecified;
            }
            set
            {
                this.showfretsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }

        
        [XmlAttribute("print-spacing")]
        public YesNo printspacing
        {
            get
            {
                return this.printspacingField;
            }
            set
            {
                this.printspacingField = value;
            }
        }

        
        [XmlIgnore]
        public bool printspacingSpecified
        {
            get
            {
                return this.printspacingFieldSpecified;
            }
            set
            {
                this.printspacingFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "staff-type")]
    public enum stafftype
    {

        
        ossia,

        
        cue,

        
        editorial,

        
        regular,

        
        alternate,
    }

    
    
    [Serializable]
    [XmlType(TypeName = "show-frets")]
    public enum showfrets
    {

        
        numbers,

        
        letters,
    }

    
    
    [Serializable]
    
    
    public class clef
    {

        private clefsign signField;

        private string lineField;

        private string clefoctavechangeField;

        private string numberField;

        private YesNo additionalField;

        private bool additionalFieldSpecified;

        private SymbolSize sizeField;

        private bool sizeFieldSpecified;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        
        public clefsign sign
        {
            get
            {
                return this.signField;
            }
            set
            {
                this.signField = value;
            }
        }

        
        [XmlElement(DataType = "integer")]
        public string line
        {
            get
            {
                return this.lineField;
            }
            set
            {
                this.lineField = value;
            }
        }

        
        [XmlElement("clef-octave-change", DataType = "integer")]
        public string clefoctavechange
        {
            get
            {
                return this.clefoctavechangeField;
            }
            set
            {
                this.clefoctavechangeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public YesNo additional
        {
            get
            {
                return this.additionalField;
            }
            set
            {
                this.additionalField = value;
            }
        }

        
        [XmlIgnore]
        public bool additionalSpecified
        {
            get
            {
                return this.additionalFieldSpecified;
            }
            set
            {
                this.additionalFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public SymbolSize size
        {
            get
            {
                return this.sizeField;
            }
            set
            {
                this.sizeField = value;
            }
        }

        
        [XmlIgnore]
        public bool sizeSpecified
        {
            get
            {
                return this.sizeFieldSpecified;
            }
            set
            {
                this.sizeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "clef-sign")]
    public enum clefsign
    {

        
        G,

        
        F,

        
        C,

        
        percussion,

        
        TAB,

        
        none,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "part-symbol")]
    public class partsymbol
    {

        private string topstaffField;

        private string bottomstaffField;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private string colorField;

        private groupsymbolvalue valueField;

        
        [XmlAttribute("top-staff", DataType = "positiveInteger")]
        public string topstaff
        {
            get
            {
                return this.topstaffField;
            }
            set
            {
                this.topstaffField = value;
            }
        }

        
        [XmlAttribute("bottom-staff", DataType = "positiveInteger")]
        public string bottomstaff
        {
            get
            {
                return this.bottomstaffField;
            }
            set
            {
                this.bottomstaffField = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlText]
        public groupsymbolvalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "group-symbol-value")]
    public enum groupsymbolvalue
    {

        
        none,

        
        brace,

        
        line,

        
        bracket,
    }

    
    
    [Serializable]
    
    
    public class time
    {

        private object[] itemsField;

        private ItemsChoiceType9[] itemsElementNameField;

        private string numberField;

        private timesymbol symbolField;

        private bool symbolFieldSpecified;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        
        [XmlElement("beat-type", typeof(string))]
        [XmlElement("beats", typeof(string))]
        [XmlElement("senza-misura", typeof(empty))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType9[] ItemsElementName
        {
            get
            {
                return this.itemsElementNameField;
            }
            set
            {
                this.itemsElementNameField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public timesymbol symbol
        {
            get
            {
                return this.symbolField;
            }
            set
            {
                this.symbolField = value;
            }
        }

        
        [XmlIgnore]
        public bool symbolSpecified
        {
            get
            {
                return this.symbolFieldSpecified;
            }
            set
            {
                this.symbolFieldSpecified = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType9
    {

        
        [XmlEnum("beat-type")]
        beattype,

        
        beats,

        
        [XmlEnum("senza-misura")]
        senzamisura,
    }

    
    
    [Serializable]
    [XmlType(TypeName = "time-symbol")]
    public enum timesymbol
    {

        
        common,

        
        cut,

        
        [XmlEnum("single-number")]
        singlenumber,

        
        normal,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "key-octave")]
    public class keyoctave
    {

        private string numberField;

        private YesNo cancelField;

        private bool cancelFieldSpecified;

        private string valueField;

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public YesNo cancel
        {
            get
            {
                return this.cancelField;
            }
            set
            {
                this.cancelField = value;
            }
        }

        
        [XmlIgnore]
        public bool cancelSpecified
        {
            get
            {
                return this.cancelFieldSpecified;
            }
            set
            {
                this.cancelFieldSpecified = value;
            }
        }

        
        [XmlTextAttribute(DataType = "integer")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class cancel
    {

        private leftright locationField;

        private bool locationFieldSpecified;

        private string valueField;

        
        [XmlAttribute]
        public leftright location
        {
            get
            {
                return this.locationField;
            }
            set
            {
                this.locationField = value;
            }
        }

        
        [XmlIgnore]
        public bool locationSpecified
        {
            get
            {
                return this.locationFieldSpecified;
            }
            set
            {
                this.locationFieldSpecified = value;
            }
        }

        
        [XmlTextAttribute(DataType = "integer")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class key
    {

        private object[] itemsField;

        private ItemsChoiceType8[] itemsElementNameField;

        private keyoctave[] keyoctaveField;

        private string numberField;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        
        [XmlElement("cancel", typeof(cancel))]
        [XmlElement("fifths", typeof(string), DataType = "integer")]
        [XmlElement("key-alter", typeof(decimal))]
        [XmlElement("key-step", typeof(Step))]
        [XmlElement("mode", typeof(string))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType8[] ItemsElementName
        {
            get
            {
                return this.itemsElementNameField;
            }
            set
            {
                this.itemsElementNameField = value;
            }
        }

        
        [XmlElement("key-octave")]
        public keyoctave[] keyoctave
        {
            get
            {
                return this.keyoctaveField;
            }
            set
            {
                this.keyoctaveField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType8
    {

        
        cancel,

        
        fifths,

        
        [XmlEnum("key-alter")]
        keyalter,

        
        [XmlEnum("key-step")]
        keystep,

        
        mode,
    }

    
    
    [Serializable]
    public class Attributes
    {
        public FormattedText footnote { get; set; }


        public Level level { get; set; }


        public decimal divisions { get; set; }


        [XmlIgnore]
        public bool divisionsSpecified { get; set; }


        [XmlElement("key")]
        public key[] key { get; set; }


        [XmlElement("time")]
        public time[] time { get; set; }


        [XmlElement(DataType = "nonNegativeInteger")]
        public string staves { get; set; }


        [XmlElement("part-symbol")]
        public partsymbol partsymbol { get; set; }


        [XmlElement(DataType = "nonNegativeInteger")]
        public string instruments { get; set; }


        [XmlElement("clef")]
        public clef[] clef { get; set; }


        [XmlElement("staff-details")]
        public staffdetails[] staffdetails { get; set; }


        public transpose transpose { get; set; }


        [XmlElement("directive")]
        public AttributesDirective[] directive { get; set; }


        [XmlElement("measure-style")]
        public measurestyle[] measurestyle { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    public class AttributesDirective
    {
        [XmlAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string lang { get; set; }


        [XmlText]
        public string Value { get; set; }
    }

    [Serializable]
    public class Sound
    {

        private midiinstrument[] midiinstrumentField;

        private Offset offsetField;

        private decimal tempoField;

        private bool tempoFieldSpecified;

        private decimal dynamicsField;

        private bool dynamicsFieldSpecified;

        private YesNo dacapoField;

        private bool dacapoFieldSpecified;

        private string segnoField;

        private string dalsegnoField;

        private string codaField;

        private string tocodaField;

        private decimal divisionsField;

        private bool divisionsFieldSpecified;

        private YesNo forwardrepeatField;

        private bool forwardrepeatFieldSpecified;

        private string fineField;

        private string timeonlyField;

        private YesNo pizzicatoField;

        private bool pizzicatoFieldSpecified;

        private decimal panField;

        private bool panFieldSpecified;

        private decimal elevationField;

        private bool elevationFieldSpecified;

        private string damperpedalField;

        private string softpedalField;

        private string sostenutopedalField;

        
        [XmlElement("midi-instrument")]
        public midiinstrument[] midiinstrument
        {
            get
            {
                return this.midiinstrumentField;
            }
            set
            {
                this.midiinstrumentField = value;
            }
        }

        
        public Offset offset
        {
            get
            {
                return this.offsetField;
            }
            set
            {
                this.offsetField = value;
            }
        }

        
        [XmlAttribute]
        public decimal tempo
        {
            get
            {
                return this.tempoField;
            }
            set
            {
                this.tempoField = value;
            }
        }

        
        [XmlIgnore]
        public bool tempoSpecified
        {
            get
            {
                return this.tempoFieldSpecified;
            }
            set
            {
                this.tempoFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal dynamics
        {
            get
            {
                return this.dynamicsField;
            }
            set
            {
                this.dynamicsField = value;
            }
        }

        
        [XmlIgnore]
        public bool dynamicsSpecified
        {
            get
            {
                return this.dynamicsFieldSpecified;
            }
            set
            {
                this.dynamicsFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo dacapo
        {
            get
            {
                return this.dacapoField;
            }
            set
            {
                this.dacapoField = value;
            }
        }

        
        [XmlIgnore]
        public bool dacapoSpecified
        {
            get
            {
                return this.dacapoFieldSpecified;
            }
            set
            {
                this.dacapoFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string segno
        {
            get
            {
                return this.segnoField;
            }
            set
            {
                this.segnoField = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string dalsegno
        {
            get
            {
                return this.dalsegnoField;
            }
            set
            {
                this.dalsegnoField = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string coda
        {
            get
            {
                return this.codaField;
            }
            set
            {
                this.codaField = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string tocoda
        {
            get
            {
                return this.tocodaField;
            }
            set
            {
                this.tocodaField = value;
            }
        }

        
        [XmlAttribute]
        public decimal divisions
        {
            get
            {
                return this.divisionsField;
            }
            set
            {
                this.divisionsField = value;
            }
        }

        
        [XmlIgnore]
        public bool divisionsSpecified
        {
            get
            {
                return this.divisionsFieldSpecified;
            }
            set
            {
                this.divisionsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("forward-repeat")]
        public YesNo forwardrepeat
        {
            get
            {
                return this.forwardrepeatField;
            }
            set
            {
                this.forwardrepeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool forwardrepeatSpecified
        {
            get
            {
                return this.forwardrepeatFieldSpecified;
            }
            set
            {
                this.forwardrepeatFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string fine
        {
            get
            {
                return this.fineField;
            }
            set
            {
                this.fineField = value;
            }
        }

        
        [XmlAttribute("time-only", DataType = "token")]
        public string timeonly
        {
            get
            {
                return this.timeonlyField;
            }
            set
            {
                this.timeonlyField = value;
            }
        }

        
        [XmlAttribute]
        public YesNo pizzicato
        {
            get
            {
                return this.pizzicatoField;
            }
            set
            {
                this.pizzicatoField = value;
            }
        }

        
        [XmlIgnore]
        public bool pizzicatoSpecified
        {
            get
            {
                return this.pizzicatoFieldSpecified;
            }
            set
            {
                this.pizzicatoFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal pan
        {
            get
            {
                return this.panField;
            }
            set
            {
                this.panField = value;
            }
        }

        
        [XmlIgnore]
        public bool panSpecified
        {
            get
            {
                return this.panFieldSpecified;
            }
            set
            {
                this.panFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal elevation
        {
            get
            {
                return this.elevationField;
            }
            set
            {
                this.elevationField = value;
            }
        }

        
        [XmlIgnore]
        public bool elevationSpecified
        {
            get
            {
                return this.elevationFieldSpecified;
            }
            set
            {
                this.elevationFieldSpecified = value;
            }
        }

        
        [XmlAttribute("damper-pedal")]
        public string damperpedal
        {
            get
            {
                return this.damperpedalField;
            }
            set
            {
                this.damperpedalField = value;
            }
        }

        
        [XmlAttribute("soft-pedal")]
        public string softpedal
        {
            get
            {
                return this.softpedalField;
            }
            set
            {
                this.softpedalField = value;
            }
        }

        
        [XmlAttribute("sostenuto-pedal")]
        public string sostenutopedal
        {
            get
            {
                return this.sostenutopedalField;
            }
            set
            {
                this.sostenutopedalField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "midi-instrument")]
    public class midiinstrument
    {

        private string midichannelField;

        private string midinameField;

        private string midibankField;

        private string midiprogramField;

        private string midiunpitchedField;

        private decimal volumeField;

        private bool volumeFieldSpecified;

        private decimal panField;

        private bool panFieldSpecified;

        private decimal elevationField;

        private bool elevationFieldSpecified;

        private string idField;

        
        [XmlElement("midi-channel", DataType = "positiveInteger")]
        public string midichannel
        {
            get
            {
                return this.midichannelField;
            }
            set
            {
                this.midichannelField = value;
            }
        }

        
        [XmlElement("midi-name")]
        public string midiname
        {
            get
            {
                return this.midinameField;
            }
            set
            {
                this.midinameField = value;
            }
        }

        
        [XmlElement("midi-bank", DataType = "positiveInteger")]
        public string midibank
        {
            get
            {
                return this.midibankField;
            }
            set
            {
                this.midibankField = value;
            }
        }

        
        [XmlElement("midi-program", DataType = "positiveInteger")]
        public string midiprogram
        {
            get
            {
                return this.midiprogramField;
            }
            set
            {
                this.midiprogramField = value;
            }
        }

        
        [XmlElement("midi-unpitched", DataType = "positiveInteger")]
        public string midiunpitched
        {
            get
            {
                return this.midiunpitchedField;
            }
            set
            {
                this.midiunpitchedField = value;
            }
        }

        
        public decimal volume
        {
            get
            {
                return this.volumeField;
            }
            set
            {
                this.volumeField = value;
            }
        }

        
        [XmlIgnore]
        public bool volumeSpecified
        {
            get
            {
                return this.volumeFieldSpecified;
            }
            set
            {
                this.volumeFieldSpecified = value;
            }
        }

        
        public decimal pan
        {
            get
            {
                return this.panField;
            }
            set
            {
                this.panField = value;
            }
        }

        
        [XmlIgnore]
        public bool panSpecified
        {
            get
            {
                return this.panFieldSpecified;
            }
            set
            {
                this.panFieldSpecified = value;
            }
        }

        
        public decimal elevation
        {
            get
            {
                return this.elevationField;
            }
            set
            {
                this.elevationField = value;
            }
        }

        
        [XmlIgnore]
        public bool elevationSpecified
        {
            get
            {
                return this.elevationFieldSpecified;
            }
            set
            {
                this.elevationFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "IDREF")]
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "other-direction")]
    public class OtherDirection
    {
        [XmlAttribute("print-object")]
        public YesNo PrintObject { get; set; }

        [XmlIgnore]
        public bool PrintObjectSpecified { get; set; }

        [XmlText]
        public string Value { get; set; }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "accordion-registration")]
    public class accordionregistration
    {

        private empty accordionhighField;

        private string accordionmiddleField;

        private empty accordionlowField;

        
        [XmlElement("accordion-high")]
        public empty accordionhigh
        {
            get
            {
                return this.accordionhighField;
            }
            set
            {
                this.accordionhighField = value;
            }
        }

        
        [XmlElement("accordion-middle", DataType = "positiveInteger")]
        public string accordionmiddle
        {
            get
            {
                return this.accordionmiddleField;
            }
            set
            {
                this.accordionmiddleField = value;
            }
        }

        
        [XmlElement("accordion-low")]
        public empty accordionlow
        {
            get
            {
                return this.accordionlowField;
            }
            set
            {
                this.accordionlowField = value;
            }
        }
    }

    
    
    [Serializable]
    public class scordatura
    {

        private accord[] accordField;

        
        [XmlElement("accord")]
        public accord[] accord
        {
            get
            {
                return this.accordField;
            }
            set
            {
                this.accordField = value;
            }
        }
    }
    
    [Serializable]
    public class accord
    {

        private Step tuningstepField;

        private decimal tuningalterField;

        private bool tuningalterFieldSpecified;

        private string tuningoctaveField;

        private string stringField;

        
        [XmlElement("tuning-step")]
        public Step tuningstep
        {
            get
            {
                return this.tuningstepField;
            }
            set
            {
                this.tuningstepField = value;
            }
        }

        
        [XmlElement("tuning-alter")]
        public decimal tuningalter
        {
            get
            {
                return this.tuningalterField;
            }
            set
            {
                this.tuningalterField = value;
            }
        }

        
        [XmlIgnore]
        public bool tuningalterSpecified
        {
            get
            {
                return this.tuningalterFieldSpecified;
            }
            set
            {
                this.tuningalterFieldSpecified = value;
            }
        }

        
        [XmlElement("tuning-octave", DataType = "integer")]
        public string tuningoctave
        {
            get
            {
                return this.tuningoctaveField;
            }
            set
            {
                this.tuningoctaveField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string @string
        {
            get
            {
                return this.stringField;
            }
            set
            {
                this.stringField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "pedal-tuning")]
    public class pedaltuning
    {

        private Step pedalstepField;

        private decimal pedalalterField;

        
        [XmlElement("pedal-step")]
        public Step pedalstep
        {
            get
            {
                return this.pedalstepField;
            }
            set
            {
                this.pedalstepField = value;
            }
        }

        
        [XmlElement("pedal-alter")]
        public decimal pedalalter
        {
            get
            {
                return this.pedalalterField;
            }
            set
            {
                this.pedalalterField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "harp-pedals")]
    public class harppedals
    {

        private pedaltuning[] pedaltuningField;

        
        [XmlElement("pedal-tuning")]
        public pedaltuning[] pedaltuning
        {
            get
            {
                return this.pedaltuningField;
            }
            set
            {
                this.pedaltuningField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "octave-shift")]
    public class octaveshift
    {

        private updownstop typeField;

        private string numberField;

        private string sizeField;

        public octaveshift()
        {
            this.sizeField = "8";
        }

        
        [XmlAttribute]
        public updownstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("8")]
        public string size
        {
            get
            {
                return this.sizeField;
            }
            set
            {
                this.sizeField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "up-down-stop")]
    public enum updownstop
    {

        
        up,

        
        down,

        
        stop,
    }

    [Serializable]
    [XmlType(TypeName = "metronome-beam")]
    public class metronomebeam
    {

        private string numberField;

        private BeamValue valueField;

        public metronomebeam()
        {
            this.numberField = "1";
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("1")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlText]
        public BeamValue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "beam-value")]
    public enum BeamValue
    {
        [XmlEnum("begin")] Begin,
        [XmlEnum("continue")] Continue, 
        [XmlEnum("end")] End,
        [XmlEnum("forward hook")] ForwardHook,
        [XmlEnum("backward hook")] BackwardHook,
    }

    [Serializable]
    [XmlType(TypeName = "metronome-note")]
    public class metronomenote
    {

        private NoteTypeValue metronometypeField;

        private empty[] metronomedotField;

        private metronomebeam[] metronomebeamField;

        private metronometuplet metronometupletField;

        
        [XmlElement("metronome-type")]
        public NoteTypeValue metronometype
        {
            get
            {
                return this.metronometypeField;
            }
            set
            {
                this.metronometypeField = value;
            }
        }

        
        [XmlElement("metronome-dot")]
        public empty[] metronomedot
        {
            get
            {
                return this.metronomedotField;
            }
            set
            {
                this.metronomedotField = value;
            }
        }

        
        [XmlElement("metronome-beam")]
        public metronomebeam[] metronomebeam
        {
            get
            {
                return this.metronomebeamField;
            }
            set
            {
                this.metronomebeamField = value;
            }
        }

        
        [XmlElement("metronome-tuplet")]
        public metronometuplet metronometuplet
        {
            get
            {
                return this.metronometupletField;
            }
            set
            {
                this.metronometupletField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "metronome-tuplet")]
    public class metronometuplet : TimeModification
    {

        private startstop typeField;

        private YesNo bracketField;

        private bool bracketFieldSpecified;

        private showtuplet shownumberField;

        private bool shownumberFieldSpecified;

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute]
        public YesNo bracket
        {
            get
            {
                return this.bracketField;
            }
            set
            {
                this.bracketField = value;
            }
        }

        
        [XmlIgnore]
        public bool bracketSpecified
        {
            get
            {
                return this.bracketFieldSpecified;
            }
            set
            {
                this.bracketFieldSpecified = value;
            }
        }

        
        [XmlAttribute("show-number")]
        public showtuplet shownumber
        {
            get
            {
                return this.shownumberField;
            }
            set
            {
                this.shownumberField = value;
            }
        }

        
        [XmlIgnore]
        public bool shownumberSpecified
        {
            get
            {
                return this.shownumberFieldSpecified;
            }
            set
            {
                this.shownumberFieldSpecified = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "show-tuplet")]
    public enum showtuplet
    {
        actual,
        both,
        none,
    }
    
    [XmlIncludeAttribute(typeof(metronometuplet))]
    [Serializable]
    [XmlType(TypeName = "time-modification")]
    public class TimeModification
    {
        private string actualnotesField;
        private string normalnotesField;
        private NoteTypeValue normaltypeField;
        private empty[] normaldotField;
        
        [XmlElement("actual-notes", DataType = "nonNegativeInteger")]
        public string actualnotes
        {
            get
            {
                return this.actualnotesField;
            }
            set
            {
                this.actualnotesField = value;
            }
        }
        
        [XmlElement("normal-notes", DataType = "nonNegativeInteger")]
        public string normalnotes
        {
            get
            {
                return this.normalnotesField;
            }
            set
            {
                this.normalnotesField = value;
            }
        }
        
        [XmlElement("normal-type")]
        public NoteTypeValue normaltype
        {
            get
            {
                return this.normaltypeField;
            }
            set
            {
                this.normaltypeField = value;
            }
        }
        
        [XmlElement("normal-dot")]
        public empty[] normaldot
        {
            get
            {
                return this.normaldotField;
            }
            set
            {
                this.normaldotField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "per-minute")]
    public class PerMinute
    {
        [XmlAttribute("font-family", DataType = "token")]
        public string FontFamily { get; set; }

        [XmlAttribute("font-style")]
        public FontStyle FontStyle { get; set; }

        [XmlIgnore]
        public bool FontStyleSpecified { get; set; }

        [XmlAttribute("font-size")]
        public string FontSize { get; set; }

        [XmlAttribute("font-weight")]
        public FontWeight FontWeight { get; set; }

        [XmlIgnore]
        public bool FontWeightSpecified { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    public class Metronome
    {
        [XmlElement("beat-unit", typeof(NoteTypeValue))]
        [XmlElement("beat-unit-dot", typeof(empty))]
        [XmlElement("metronome-note", typeof(metronomenote))]
        [XmlElement("metronome-relation", typeof(string))]
        [XmlElement("per-minute", typeof(PerMinute))]
        public object[] Items { get; set; }

        [XmlAttribute("parentheses")]
        public YesNo Parentheses { get; set; }

        [XmlIgnore]
        public bool ParenthesesSpecified { get; set; }

        public Metronome()
        {
            Items = new object[2];
            Items[0] = NoteTypeValue.Chrotchet;
            Items[1] = new PerMinute
            {
                FontSize = "11.9365",
                FontFamily = "Opus Text Std",
                FontStyle = FontStyle.Italic,
                FontWeight = FontWeight.Bold,
                Value = "100"
            };
        }

        [XmlIgnore]
        public int BeatPerMinute
        {
            get { return Convert.ToInt32(((PerMinute) Items[1]).Value); }
            set { ((PerMinute) Items[1]).Value = value.ToString(CultureInfo.InvariantCulture); }
        }
    }

    [Serializable]
    public class pedal
    {
        [XmlAttribute]
        public startstopchange type { get; set; }

        [XmlAttribute]
        public YesNo line { get; set; }

        [XmlIgnore]
        public bool lineSpecified { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "start-stop-change")]
    public enum startstopchange
    {
        start,
        stop,
        change,
    }
    
    [Serializable]
    public class bracket
    {
        private startstop typeField;
        private string numberField;
        private lineend lineendField;
        private decimal endlengthField;
        private bool endlengthFieldSpecified;

        private linetype linetypeField;

        private bool linetypeFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private string colorField;

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("line-end")]
        public lineend lineend
        {
            get
            {
                return this.lineendField;
            }
            set
            {
                this.lineendField = value;
            }
        }

        
        [XmlAttribute("end-length")]
        public decimal endlength
        {
            get
            {
                return this.endlengthField;
            }
            set
            {
                this.endlengthField = value;
            }
        }

        
        [XmlIgnore]
        public bool endlengthSpecified
        {
            get
            {
                return this.endlengthFieldSpecified;
            }
            set
            {
                this.endlengthFieldSpecified = value;
            }
        }

        
        [XmlAttribute("line-type")]
        public linetype linetype
        {
            get
            {
                return this.linetypeField;
            }
            set
            {
                this.linetypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool linetypeSpecified
        {
            get
            {
                return this.linetypeFieldSpecified;
            }
            set
            {
                this.linetypeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "line-end")]
    public enum lineend
    {

        
        up,

        
        down,

        
        both,

        
        arrow,

        
        none,
    }

    
    
    [Serializable]
    [XmlType(TypeName = "line-type")]
    public enum linetype
    {

        
        solid,

        
        dashed,

        
        dotted,

        
        wavy,
    }

    
    
    [Serializable]
    
    
    public class dashes
    {

        private startstop typeField;

        private string numberField;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private string colorField;

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class wedge
    {

        private wedgetype typeField;

        private string numberField;

        private decimal spreadField;

        private bool spreadFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private string colorField;

        
        [XmlAttribute]
        public wedgetype type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public decimal spread
        {
            get
            {
                return this.spreadField;
            }
            set
            {
                this.spreadField = value;
            }
        }

        
        [XmlIgnore]
        public bool spreadSpecified
        {
            get
            {
                return this.spreadFieldSpecified;
            }
            set
            {
                this.spreadFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "wedge-type")]
    public enum wedgetype
    {

        
        crescendo,

        
        diminuendo,

        
        stop,
    }

    
    
    [Serializable]
    
    
    public class rehearsal
    {

        private string underlineField;

        private string overlineField;

        private string linethroughField;

        private string langField;

        private textdirection dirField;

        private bool dirFieldSpecified;

        private decimal rotationField;

        private bool rotationFieldSpecified;

        private rehearsalenclosure enclosureField;

        private bool enclosureFieldSpecified;

        private string valueField;

        
        [XmlAttribute(DataType = "nonNegativeInteger")]
        public string underline
        {
            get
            {
                return this.underlineField;
            }
            set
            {
                this.underlineField = value;
            }
        }

        
        [XmlAttribute(DataType = "nonNegativeInteger")]
        public string overline
        {
            get
            {
                return this.overlineField;
            }
            set
            {
                this.overlineField = value;
            }
        }

        
        [XmlAttribute("line-through", DataType = "nonNegativeInteger")]
        public string linethrough
        {
            get
            {
                return this.linethroughField;
            }
            set
            {
                this.linethroughField = value;
            }
        }

        
        [XmlAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string lang
        {
            get
            {
                return this.langField;
            }
            set
            {
                this.langField = value;
            }
        }

        
        [XmlAttribute]
        public textdirection dir
        {
            get
            {
                return this.dirField;
            }
            set
            {
                this.dirField = value;
            }
        }

        
        [XmlIgnore]
        public bool dirSpecified
        {
            get
            {
                return this.dirFieldSpecified;
            }
            set
            {
                this.dirFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal rotation
        {
            get
            {
                return this.rotationField;
            }
            set
            {
                this.rotationField = value;
            }
        }

        
        [XmlIgnore]
        public bool rotationSpecified
        {
            get
            {
                return this.rotationFieldSpecified;
            }
            set
            {
                this.rotationFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public rehearsalenclosure enclosure
        {
            get
            {
                return this.enclosureField;
            }
            set
            {
                this.enclosureField = value;
            }
        }

        
        [XmlIgnore]
        public bool enclosureSpecified
        {
            get
            {
                return this.enclosureFieldSpecified;
            }
            set
            {
                this.enclosureFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "text-direction")]
    public enum textdirection
    {

        
        ltr,

        
        rtl,

        
        lro,

        
        rlo,
    }

    
    
    [Serializable]
    [XmlType(TypeName = "rehearsal-enclosure")]
    public enum rehearsalenclosure
    {

        
        square,

        
        circle,

        
        none,
    }
    
    [Serializable]
    [XmlType(TypeName = "direction-type")]
    public class DirectionType
    {
        [XmlElement("accordion-registration", typeof(accordionregistration))]
        [XmlElement("bracket", typeof(bracket))]
        [XmlElement("coda", typeof(emptyprintstyle))]
        [XmlElement("damp", typeof(emptyprintstyle))]
        [XmlElement("damp-all", typeof(emptyprintstyle))]
        [XmlElement("dashes", typeof(dashes))]
        [XmlElement("dynamics", typeof(dynamics))]
        [XmlElement("eyeglasses", typeof(emptyprintstyle))]
        [XmlElement("harp-pedals", typeof(harppedals))]
        [XmlElement("image", typeof(image))]
        [XmlElement("metronome", typeof(Metronome))]
        [XmlElement("octave-shift", typeof(octaveshift))]
        [XmlElement("other-direction", typeof(OtherDirection))]
        [XmlElement("pedal", typeof(pedal))]
        [XmlElement("rehearsal", typeof(rehearsal))]
        [XmlElement("scordatura", typeof(scordatura))]
        [XmlElement("segno", typeof(emptyprintstyle))]
        [XmlElement("wedge", typeof(wedge))]
        [XmlElement("words", typeof(FormattedText))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items { get; set; }

        [XmlElement("ItemsElementName"), XmlIgnore]
        public ItemsChoiceType7[] ItemsElementName { get; set; }

        internal void SetBeatValue(int value)
        {
            ((Metronome) Items[0]).BeatPerMinute = value;
        }

        public DirectionType()
        {
            Items = new object[] {new Metronome()};
            ItemsElementName = new[] {ItemsChoiceType7.Metronome};
        }
    }
    
    [Serializable]
    public class dynamics
    {
        private object[] itemsField;

        [XmlElement("f", typeof(empty))]
        [XmlElement("ff", typeof(empty))]
        [XmlElement("fff", typeof(empty))]
        [XmlElement("ffff", typeof(empty))]
        [XmlElement("fffff", typeof(empty))]
        [XmlElement("ffffff", typeof(empty))]
        [XmlElement("fp", typeof(empty))]
        [XmlElement("fz", typeof(empty))]
        [XmlElement("mf", typeof(empty))]
        [XmlElement("mp", typeof(empty))]
        [XmlElement("other-dynamics", typeof(string))]
        [XmlElement("p", typeof(empty))]
        [XmlElement("pp", typeof(empty))]
        [XmlElement("ppp", typeof(empty))]
        [XmlElement("pppp", typeof(empty))]
        [XmlElement("ppppp", typeof(empty))]
        [XmlElement("pppppp", typeof(empty))]
        [XmlElement("rf", typeof(empty))]
        [XmlElement("rfz", typeof(empty))]
        [XmlElement("sf", typeof(empty))]
        [XmlElement("sffz", typeof(empty))]
        [XmlElement("sfp", typeof(empty))]
        [XmlElement("sfpp", typeof(empty))]
        [XmlElement("sfz", typeof(empty))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        [XmlElement("ItemsElementName"), XmlIgnore]
        public ItemsChoiceType5[] ItemsElementName { get; set; }

        [XmlAttribute]
        public AboveBelow placement { get; set; }

        [XmlIgnore]
        public bool placementSpecified { get; set; }
    }
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType5
    {
        f, ff, fff, ffff, fffff, ffffff, fp,fz, mf, mp,

        [XmlEnum("other-dynamics")]
        otherdynamics,
        
        p, pp, ppp, pppp, ppppp, pppppp,
        
        rf, rfz, sf, sffz, sfp, sfpp, sfz,
    }

    [Serializable]
    public class image
    {
        private string sourceField;
        private string typeField;

        
        [XmlAttribute(DataType = "anyURI")]
        public string source
        {
            get
            {
                return this.sourceField;
            }
            set
            {
                this.sourceField = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType7
    {
        [XmlEnum("accordion-registration")] AccordionRegistration,
        [XmlEnum("bracket")] Bracket,
        [XmlEnum("coda")] Coda,
        [XmlEnum("damp")] Damp,
        [XmlEnum("damp-all")] DampAll,
        [XmlEnum("dashes")] Dashes,
        [XmlEnum("dynamics")] Dynamics,
        [XmlEnum("eyeglasses")] EyeGlasses,
        [XmlEnum("harp-pedals")] HarpPedals,
        [XmlEnum("image")] Image,
        [XmlEnum("metronome")] Metronome,
        [XmlEnum("octave-shift")] OctaveShift,
        [XmlEnum("other-direction")] OtherDirection,
        [XmlEnum("pedal")] Pedal,
        [XmlEnum("rehearsal")] Rehearsal,
        [XmlEnum("scordatura")] Scordatura,
        [XmlEnum("segno")] Segno,
        [XmlEnum("wedge")] Wedge,
        [XmlEnum("words")] Words
    }

    [Serializable]
    [XmlType(TypeName = "direction")]
    public class Direction
    {
        [XmlElement("direction-type")]
        public DirectionType[] DirectionType { get; set; }

        [XmlElement("offset")]
        public Offset Offset { get; set; }

        [XmlElement("footnote")]
        public FormattedText Footnote { get; set; }

        [XmlElement("level")]
        public Level Level { get; set; }

        [XmlElement("voice")]
        public string Voice { get; set; }

        [XmlElement("staff", DataType = "positiveInteger")]
        public string Staff { get; set; }

        [XmlElement("sound")]
        public Sound Sound { get; set; }

        [XmlAttribute("placement")]
        public AboveBelow Placement { get; set; }

        [XmlIgnore]
        public bool PlacementSpecified { get; set; }

        [XmlAttribute("directive")]
        public YesNo Directive { get; set; }

        [XmlIgnore]
        public bool DirectiveSpecified { get; set; }

        public void SetBeatPerMinute(int value)
        {
            DirectionType[0].SetBeatValue(value);
        }

        public Direction()
        {
            Voice = "1";
            Staff = "1";
            DirectionType = new[] {new DirectionType()};
        }
    }
    
    [Serializable]
    public class forward
    {

        private decimal durationField;

        private FormattedText footnoteField;

        private Level levelField;

        private string voiceField;

        private string staffField;

        
        public decimal duration
        {
            get
            {
                return this.durationField;
            }
            set
            {
                this.durationField = value;
            }
        }

        
        public FormattedText footnote
        {
            get
            {
                return this.footnoteField;
            }
            set
            {
                this.footnoteField = value;
            }
        }

        
        public Level level
        {
            get
            {
                return this.levelField;
            }
            set
            {
                this.levelField = value;
            }
        }

        
        public string voice
        {
            get
            {
                return this.voiceField;
            }
            set
            {
                this.voiceField = value;
            }
        }

        
        [XmlElement(DataType = "positiveInteger")]
        public string staff
        {
            get
            {
                return this.staffField;
            }
            set
            {
                this.staffField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class backup
    {

        private decimal durationField;

        private FormattedText footnoteField;

        private Level levelField;

        
        public decimal duration
        {
            get
            {
                return this.durationField;
            }
            set
            {
                this.durationField = value;
            }
        }

        
        public FormattedText footnote
        {
            get
            {
                return this.footnoteField;
            }
            set
            {
                this.footnoteField = value;
            }
        }

        
        public Level level
        {
            get
            {
                return this.levelField;
            }
            set
            {
                this.levelField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class elision
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        private string valueField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "text-element-data")]
    public class textelementdata
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        private string underlineField;

        private string overlineField;

        private string linethroughField;

        private decimal rotationField;

        private bool rotationFieldSpecified;

        private string letterspacingField;

        private string langField;

        private textdirection dirField;

        private bool dirFieldSpecified;

        private string valueField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlAttribute(DataType = "nonNegativeInteger")]
        public string underline
        {
            get
            {
                return this.underlineField;
            }
            set
            {
                this.underlineField = value;
            }
        }

        
        [XmlAttribute(DataType = "nonNegativeInteger")]
        public string overline
        {
            get
            {
                return this.overlineField;
            }
            set
            {
                this.overlineField = value;
            }
        }

        
        [XmlAttribute("line-through", DataType = "nonNegativeInteger")]
        public string linethrough
        {
            get
            {
                return this.linethroughField;
            }
            set
            {
                this.linethroughField = value;
            }
        }

        
        [XmlAttribute]
        public decimal rotation
        {
            get
            {
                return this.rotationField;
            }
            set
            {
                this.rotationField = value;
            }
        }

        
        [XmlIgnore]
        public bool rotationSpecified
        {
            get
            {
                return this.rotationFieldSpecified;
            }
            set
            {
                this.rotationFieldSpecified = value;
            }
        }

        
        [XmlAttribute("letter-spacing")]
        public string letterspacing
        {
            get
            {
                return this.letterspacingField;
            }
            set
            {
                this.letterspacingField = value;
            }
        }

        
        [XmlAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string lang
        {
            get
            {
                return this.langField;
            }
            set
            {
                this.langField = value;
            }
        }

        
        [XmlAttribute]
        public textdirection dir
        {
            get
            {
                return this.dirField;
            }
            set
            {
                this.dirField = value;
            }
        }

        
        [XmlIgnore]
        public bool dirSpecified
        {
            get
            {
                return this.dirFieldSpecified;
            }
            set
            {
                this.dirFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class lyric
    {

        private object[] itemsField;

        private ItemsChoiceType6[] itemsElementNameField;

        private empty endlineField;

        private empty endparagraphField;

        private FormattedText footnoteField;

        private Level levelField;

        private string numberField;

        private string nameField;

        private leftcenterright justifyField;

        private bool justifyFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string colorField;

        
        [XmlElement("elision", typeof(elision))]
        [XmlElement("extend", typeof(extend))]
        [XmlElement("humming", typeof(empty))]
        [XmlElement("laughing", typeof(empty))]
        [XmlElement("syllabic", typeof(syllabic))]
        [XmlElement("text", typeof(textelementdata))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType6[] ItemsElementName
        {
            get
            {
                return this.itemsElementNameField;
            }
            set
            {
                this.itemsElementNameField = value;
            }
        }

        
        [XmlElement("end-line")]
        public empty endline
        {
            get
            {
                return this.endlineField;
            }
            set
            {
                this.endlineField = value;
            }
        }

        
        [XmlElement("end-paragraph")]
        public empty endparagraph
        {
            get
            {
                return this.endparagraphField;
            }
            set
            {
                this.endparagraphField = value;
            }
        }

        
        public FormattedText footnote
        {
            get
            {
                return this.footnoteField;
            }
            set
            {
                this.footnoteField = value;
            }
        }

        
        public Level level
        {
            get
            {
                return this.levelField;
            }
            set
            {
                this.levelField = value;
            }
        }

        
        [XmlAttribute(DataType = "NMTOKEN")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        
        [XmlAttribute]
        public leftcenterright justify
        {
            get
            {
                return this.justifyField;
            }
            set
            {
                this.justifyField = value;
            }
        }

        
        [XmlIgnore]
        public bool justifySpecified
        {
            get
            {
                return this.justifyFieldSpecified;
            }
            set
            {
                this.justifyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    public enum syllabic
    {

        
        single,

        
        begin,

        
        end,

        
        middle,
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType6
    {

        
        elision,

        
        extend,

        
        humming,

        
        laughing,

        
        syllabic,

        
        text,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "other-notation")]
    public class othernotation
    {

        private StartStopSingle typeField;

        private string numberField;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string valueField;

        public othernotation()
        {
            this.numberField = "1";
        }

        
        [XmlAttribute]
        public StartStopSingle type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("1")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "non-arpeggiate")]
    public class nonarpeggiate
    {

        private topbottom typeField;

        private string numberField;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string colorField;

        
        [XmlAttribute]
        public topbottom type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "top-bottom")]
    public enum topbottom
    {

        
        top,

        
        bottom,
    }

    
    
    [Serializable]
    
    
    public class arpeggiate
    {

        private string numberField;

        private updown directionField;

        private bool directionFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string colorField;

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public updown direction
        {
            get
            {
                return this.directionField;
            }
            set
            {
                this.directionField = value;
            }
        }

        
        [XmlIgnore]
        public bool directionSpecified
        {
            get
            {
                return this.directionFieldSpecified;
            }
            set
            {
                this.directionFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "up-down")]
    public enum updown
    {

        
        up,

        
        down,
    }

    
    
    [Serializable]
    
    
    public class articulations
    {

        private object[] itemsField;

        private ItemsChoiceType4[] itemsElementNameField;

        
        [XmlElement("accent", typeof(EmptyPlacement))]
        [XmlElement("breath-mark", typeof(EmptyPlacement))]
        [XmlElement("caesura", typeof(EmptyPlacement))]
        [XmlElement("detached-legato", typeof(EmptyPlacement))]
        [XmlElement("doit", typeof(emptyline))]
        [XmlElement("falloff", typeof(emptyline))]
        [XmlElement("other-articulation", typeof(placementtext))]
        [XmlElement("plop", typeof(emptyline))]
        [XmlElement("scoop", typeof(emptyline))]
        [XmlElement("spiccato", typeof(EmptyPlacement))]
        [XmlElement("staccatissimo", typeof(EmptyPlacement))]
        [XmlElement("staccato", typeof(EmptyPlacement))]
        [XmlElement("stress", typeof(EmptyPlacement))]
        [XmlElement("strong-accent", typeof(strongaccent))]
        [XmlElement("tenuto", typeof(EmptyPlacement))]
        [XmlElement("unstress", typeof(EmptyPlacement))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType4[] ItemsElementName
        {
            get
            {
                return this.itemsElementNameField;
            }
            set
            {
                this.itemsElementNameField = value;
            }
        }
    }

    
    [XmlIncludeAttribute(typeof(strongaccent))]
    [XmlIncludeAttribute(typeof(heeltoe))]
    
    [Serializable]
    
    
    [XmlType(TypeName = "empty-placement")]
    public class EmptyPlacement
    {

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "strong-accent")]
    public class strongaccent : EmptyPlacement
    {

        private updown typeField;

        public strongaccent()
        {
            this.typeField = updown.up;
        }

        
        [XmlAttribute]
        [DefaultValueAttribute(updown.up)]
        public updown type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "heel-toe")]
    public class heeltoe : EmptyPlacement
    {

        private YesNo substitutionField;

        private bool substitutionFieldSpecified;

        
        [XmlAttribute]
        public YesNo substitution
        {
            get
            {
                return this.substitutionField;
            }
            set
            {
                this.substitutionField = value;
            }
        }

        
        [XmlIgnore]
        public bool substitutionSpecified
        {
            get
            {
                return this.substitutionFieldSpecified;
            }
            set
            {
                this.substitutionFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "empty-line")]
    public class emptyline
    {

        private lineshape lineshapeField;

        private bool lineshapeFieldSpecified;

        private linetype linetypeField;

        private bool linetypeFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        
        [XmlAttribute("line-shape")]
        public lineshape lineshape
        {
            get
            {
                return this.lineshapeField;
            }
            set
            {
                this.lineshapeField = value;
            }
        }

        
        [XmlIgnore]
        public bool lineshapeSpecified
        {
            get
            {
                return this.lineshapeFieldSpecified;
            }
            set
            {
                this.lineshapeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("line-type")]
        public linetype linetype
        {
            get
            {
                return this.linetypeField;
            }
            set
            {
                this.linetypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool linetypeSpecified
        {
            get
            {
                return this.linetypeFieldSpecified;
            }
            set
            {
                this.linetypeFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "line-shape")]
    public enum lineshape
    {

        
        straight,

        
        curved,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "placement-text")]
    public class placementtext
    {

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string valueField;

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType4
    {

        
        accent,

        
        [XmlEnum("breath-mark")]
        breathmark,

        
        caesura,

        
        [XmlEnum("detached-legato")]
        detachedlegato,

        
        doit,

        
        falloff,

        
        [XmlEnum("other-articulation")]
        otherarticulation,

        
        plop,

        
        scoop,

        
        spiccato,

        
        staccatissimo,

        
        staccato,

        
        stress,

        
        [XmlEnum("strong-accent")]
        strongaccent,

        
        tenuto,

        
        unstress,
    }

    
    
    [Serializable]
    
    
    public class technical
    {

        private object[] itemsField;

        private ItemsChoiceType3[] itemsElementNameField;

        
        [XmlElement("bend", typeof(bend))]
        [XmlElement("double-tongue", typeof(EmptyPlacement))]
        [XmlElement("down-bow", typeof(EmptyPlacement))]
        [XmlElement("fingering", typeof(fingering))]
        [XmlElement("fingernails", typeof(EmptyPlacement))]
        [XmlElement("fret", typeof(fret))]
        [XmlElement("hammer-on", typeof(hammeronpulloff))]
        [XmlElement("harmonic", typeof(harmonic))]
        [XmlElement("heel", typeof(heeltoe))]
        [XmlElement("open-string", typeof(EmptyPlacement))]
        [XmlElement("other-technical", typeof(placementtext))]
        [XmlElement("pluck", typeof(placementtext))]
        [XmlElement("pull-off", typeof(hammeronpulloff))]
        [XmlElement("snap-pizzicato", typeof(EmptyPlacement))]
        [XmlElement("stopped", typeof(EmptyPlacement))]
        [XmlElement("string", typeof(String))]
        [XmlElement("tap", typeof(placementtext))]
        [XmlElement("thumb-position", typeof(EmptyPlacement))]
        [XmlElement("toe", typeof(heeltoe))]
        [XmlElement("triple-tongue", typeof(EmptyPlacement))]
        [XmlElement("up-bow", typeof(EmptyPlacement))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType3[] ItemsElementName
        {
            get
            {
                return this.itemsElementNameField;
            }
            set
            {
                this.itemsElementNameField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class bend
    {

        private decimal bendalterField;

        private empty itemField;

        private ItemChoiceType1 itemElementNameField;

        private placementtext withbarField;

        private YesNo accelerateField;

        private bool accelerateFieldSpecified;

        private decimal beatsField;

        private bool beatsFieldSpecified;

        private decimal firstbeatField;

        private bool firstbeatFieldSpecified;

        private decimal lastbeatField;

        private bool lastbeatFieldSpecified;

        
        [XmlElement("bend-alter")]
        public decimal bendalter
        {
            get
            {
                return this.bendalterField;
            }
            set
            {
                this.bendalterField = value;
            }
        }

        
        [XmlElement("pre-bend", typeof(empty))]
        [XmlElement("release", typeof(empty))]
        [XmlChoiceIdentifierAttribute("ItemElementName")]
        public empty Item
        {
            get
            {
                return this.itemField;
            }
            set
            {
                this.itemField = value;
            }
        }

        
        [XmlIgnore]
        public ItemChoiceType1 ItemElementName
        {
            get
            {
                return this.itemElementNameField;
            }
            set
            {
                this.itemElementNameField = value;
            }
        }

        
        [XmlElement("with-bar")]
        public placementtext withbar
        {
            get
            {
                return this.withbarField;
            }
            set
            {
                this.withbarField = value;
            }
        }

        
        [XmlAttribute]
        public YesNo accelerate
        {
            get
            {
                return this.accelerateField;
            }
            set
            {
                this.accelerateField = value;
            }
        }

        
        [XmlIgnore]
        public bool accelerateSpecified
        {
            get
            {
                return this.accelerateFieldSpecified;
            }
            set
            {
                this.accelerateFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal beats
        {
            get
            {
                return this.beatsField;
            }
            set
            {
                this.beatsField = value;
            }
        }

        
        [XmlIgnore]
        public bool beatsSpecified
        {
            get
            {
                return this.beatsFieldSpecified;
            }
            set
            {
                this.beatsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("first-beat")]
        public decimal firstbeat
        {
            get
            {
                return this.firstbeatField;
            }
            set
            {
                this.firstbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool firstbeatSpecified
        {
            get
            {
                return this.firstbeatFieldSpecified;
            }
            set
            {
                this.firstbeatFieldSpecified = value;
            }
        }

        
        [XmlAttribute("last-beat")]
        public decimal lastbeat
        {
            get
            {
                return this.lastbeatField;
            }
            set
            {
                this.lastbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool lastbeatSpecified
        {
            get
            {
                return this.lastbeatFieldSpecified;
            }
            set
            {
                this.lastbeatFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemChoiceType1
    {

        
        [XmlEnum("pre-bend")]
        prebend,

        
        release,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "hammer-on-pull-off")]
    public class hammeronpulloff
    {

        private startstop typeField;

        private string numberField;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private string valueField;

        public hammeronpulloff()
        {
            this.numberField = "1";
        }

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("1")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class harmonic
    {

        private empty itemField;

        private ItemChoiceType itemElementNameField;

        private empty item1Field;

        private Item1ChoiceType item1ElementNameField;

        private YesNo printobjectField;

        private bool printobjectFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        
        [XmlElement("artificial", typeof(empty))]
        [XmlElement("natural", typeof(empty))]
        [XmlChoiceIdentifierAttribute("ItemElementName")]
        public empty Item
        {
            get
            {
                return this.itemField;
            }
            set
            {
                this.itemField = value;
            }
        }

        
        [XmlIgnore]
        public ItemChoiceType ItemElementName
        {
            get
            {
                return this.itemElementNameField;
            }
            set
            {
                this.itemElementNameField = value;
            }
        }

        
        [XmlElement("base-pitch", typeof(empty))]
        [XmlElement("sounding-pitch", typeof(empty))]
        [XmlElement("touching-pitch", typeof(empty))]
        [XmlChoiceIdentifierAttribute("Item1ElementName")]
        public empty Item1
        {
            get
            {
                return this.item1Field;
            }
            set
            {
                this.item1Field = value;
            }
        }

        
        [XmlIgnore]
        public Item1ChoiceType Item1ElementName
        {
            get
            {
                return this.item1ElementNameField;
            }
            set
            {
                this.item1ElementNameField = value;
            }
        }

        
        [XmlAttribute("print-object")]
        public YesNo printobject
        {
            get
            {
                return this.printobjectField;
            }
            set
            {
                this.printobjectField = value;
            }
        }

        
        [XmlIgnore]
        public bool printobjectSpecified
        {
            get
            {
                return this.printobjectFieldSpecified;
            }
            set
            {
                this.printobjectFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemChoiceType
    {

        
        artificial,

        
        natural,
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum Item1ChoiceType
    {

        
        [XmlEnum("base-pitch")]
        basepitch,

        
        [XmlEnum("sounding-pitch")]
        soundingpitch,

        
        [XmlEnum("touching-pitch")]
        touchingpitch,
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType3
    {

        
        bend,

        
        [XmlEnum("double-tongue")]
        doubletongue,

        
        [XmlEnum("down-bow")]
        downbow,

        
        fingering,

        
        fingernails,

        
        fret,

        
        [XmlEnum("hammer-on")]
        hammeron,

        
        harmonic,

        
        heel,

        
        [XmlEnum("open-string")]
        openstring,

        
        [XmlEnum("other-technical")]
        othertechnical,

        
        pluck,

        
        [XmlEnum("pull-off")]
        pulloff,

        
        [XmlEnum("snap-pizzicato")]
        snappizzicato,

        
        stopped,

        
        @string,

        
        tap,

        
        [XmlEnum("thumb-position")]
        thumbposition,

        
        toe,

        
        [XmlEnum("triple-tongue")]
        tripletongue,

        
        [XmlEnum("up-bow")]
        upbow,
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "accidental-mark")]
    public class accidentalmark
    {

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private accidentalvalue valueField;

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlText]
        public accidentalvalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    public class Tremolo
    {
        [XmlAttribute("type"), DefaultValue(StartStopSingle.Single)]
        public StartStopSingle Type { get; set; }

        [XmlAttribute("placement")]
        public AboveBelow Placement { get; set; }
        
        [XmlIgnore]
        public bool PlacementSpecified { get; set; }

        [XmlText(DataType = "integer")]
        public string Value { get; set; }
    }
    
    [XmlIncludeAttribute(typeof(mordent))]
    [Serializable]
    [XmlType(TypeName = "empty-trill-sound")]
    public class emptytrillsound
    {

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private startnote startnoteField;

        private bool startnoteFieldSpecified;

        private trillstep trillstepField;

        private bool trillstepFieldSpecified;

        private twonoteturn twonoteturnField;

        private bool twonoteturnFieldSpecified;

        private YesNo accelerateField;

        private bool accelerateFieldSpecified;

        private decimal beatsField;

        private bool beatsFieldSpecified;

        private decimal secondbeatField;

        private bool secondbeatFieldSpecified;

        private decimal lastbeatField;

        private bool lastbeatFieldSpecified;

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute("start-note")]
        public startnote startnote
        {
            get
            {
                return this.startnoteField;
            }
            set
            {
                this.startnoteField = value;
            }
        }

        
        [XmlIgnore]
        public bool startnoteSpecified
        {
            get
            {
                return this.startnoteFieldSpecified;
            }
            set
            {
                this.startnoteFieldSpecified = value;
            }
        }

        
        [XmlAttribute("trill-step")]
        public trillstep trillstep
        {
            get
            {
                return this.trillstepField;
            }
            set
            {
                this.trillstepField = value;
            }
        }

        
        [XmlIgnore]
        public bool trillstepSpecified
        {
            get
            {
                return this.trillstepFieldSpecified;
            }
            set
            {
                this.trillstepFieldSpecified = value;
            }
        }

        
        [XmlAttribute("two-note-turn")]
        public twonoteturn twonoteturn
        {
            get
            {
                return this.twonoteturnField;
            }
            set
            {
                this.twonoteturnField = value;
            }
        }

        
        [XmlIgnore]
        public bool twonoteturnSpecified
        {
            get
            {
                return this.twonoteturnFieldSpecified;
            }
            set
            {
                this.twonoteturnFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo accelerate
        {
            get
            {
                return this.accelerateField;
            }
            set
            {
                this.accelerateField = value;
            }
        }

        
        [XmlIgnore]
        public bool accelerateSpecified
        {
            get
            {
                return this.accelerateFieldSpecified;
            }
            set
            {
                this.accelerateFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal beats
        {
            get
            {
                return this.beatsField;
            }
            set
            {
                this.beatsField = value;
            }
        }

        
        [XmlIgnore]
        public bool beatsSpecified
        {
            get
            {
                return this.beatsFieldSpecified;
            }
            set
            {
                this.beatsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("second-beat")]
        public decimal secondbeat
        {
            get
            {
                return this.secondbeatField;
            }
            set
            {
                this.secondbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool secondbeatSpecified
        {
            get
            {
                return this.secondbeatFieldSpecified;
            }
            set
            {
                this.secondbeatFieldSpecified = value;
            }
        }

        
        [XmlAttribute("last-beat")]
        public decimal lastbeat
        {
            get
            {
                return this.lastbeatField;
            }
            set
            {
                this.lastbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool lastbeatSpecified
        {
            get
            {
                return this.lastbeatFieldSpecified;
            }
            set
            {
                this.lastbeatFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class mordent : emptytrillsound
    {

        private YesNo longField;

        private bool longFieldSpecified;

        
        [XmlAttribute]
        public YesNo @long
        {
            get
            {
                return this.longField;
            }
            set
            {
                this.longField = value;
            }
        }

        
        [XmlIgnore]
        public bool longSpecified
        {
            get
            {
                return this.longFieldSpecified;
            }
            set
            {
                this.longFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class ornaments
    {

        private object[] itemsField;

        private ItemsChoiceType2[] itemsElementNameField;

        private accidentalmark[] accidentalmarkField;

        
        [XmlElement("delayed-turn", typeof(emptytrillsound))]
        [XmlElement("inverted-mordent", typeof(mordent))]
        [XmlElement("inverted-turn", typeof(emptytrillsound))]
        [XmlElement("mordent", typeof(mordent))]
        [XmlElement("other-ornament", typeof(placementtext))]
        [XmlElement("schleifer", typeof(EmptyPlacement))]
        [XmlElement("shake", typeof(emptytrillsound))]
        [XmlElement("tremolo", typeof(Tremolo))]
        [XmlElement("trill-mark", typeof(emptytrillsound))]
        [XmlElement("turn", typeof(emptytrillsound))]
        [XmlElement("wavy-line", typeof(WavyLine))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        
        [XmlElement("ItemsElementName")]
        [XmlIgnore]
        public ItemsChoiceType2[] ItemsElementName
        {
            get
            {
                return this.itemsElementNameField;
            }
            set
            {
                this.itemsElementNameField = value;
            }
        }

        
        [XmlElement("accidental-mark")]
        public accidentalmark[] accidentalmark
        {
            get
            {
                return this.accidentalmarkField;
            }
            set
            {
                this.accidentalmarkField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType2
    {

        
        [XmlEnum("delayed-turn")]
        delayedturn,

        
        [XmlEnum("inverted-mordent")]
        invertedmordent,

        
        [XmlEnum("inverted-turn")]
        invertedturn,

        
        mordent,

        
        [XmlEnum("other-ornament")]
        otherornament,

        
        schleifer,

        
        shake,

        
        tremolo,

        
        [XmlEnum("trill-mark")]
        trillmark,

        
        turn,

        
        [XmlEnum("wavy-line")]
        wavyline,
    }

    
    
    [Serializable]
    
    
    public class slide
    {

        private startstop typeField;

        private string numberField;

        private linetype linetypeField;

        private bool linetypeFieldSpecified;

        private YesNo accelerateField;

        private bool accelerateFieldSpecified;

        private decimal beatsField;

        private bool beatsFieldSpecified;

        private decimal firstbeatField;

        private bool firstbeatFieldSpecified;

        private decimal lastbeatField;

        private bool lastbeatFieldSpecified;

        private string valueField;

        public slide()
        {
            this.numberField = "1";
        }

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("1")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("line-type")]
        public linetype linetype
        {
            get
            {
                return this.linetypeField;
            }
            set
            {
                this.linetypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool linetypeSpecified
        {
            get
            {
                return this.linetypeFieldSpecified;
            }
            set
            {
                this.linetypeFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo accelerate
        {
            get
            {
                return this.accelerateField;
            }
            set
            {
                this.accelerateField = value;
            }
        }

        
        [XmlIgnore]
        public bool accelerateSpecified
        {
            get
            {
                return this.accelerateFieldSpecified;
            }
            set
            {
                this.accelerateFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public decimal beats
        {
            get
            {
                return this.beatsField;
            }
            set
            {
                this.beatsField = value;
            }
        }

        
        [XmlIgnore]
        public bool beatsSpecified
        {
            get
            {
                return this.beatsFieldSpecified;
            }
            set
            {
                this.beatsFieldSpecified = value;
            }
        }

        
        [XmlAttribute("first-beat")]
        public decimal firstbeat
        {
            get
            {
                return this.firstbeatField;
            }
            set
            {
                this.firstbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool firstbeatSpecified
        {
            get
            {
                return this.firstbeatFieldSpecified;
            }
            set
            {
                this.firstbeatFieldSpecified = value;
            }
        }

        
        [XmlAttribute("last-beat")]
        public decimal lastbeat
        {
            get
            {
                return this.lastbeatField;
            }
            set
            {
                this.lastbeatField = value;
            }
        }

        
        [XmlIgnore]
        public bool lastbeatSpecified
        {
            get
            {
                return this.lastbeatFieldSpecified;
            }
            set
            {
                this.lastbeatFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class glissando
    {

        private startstop typeField;

        private string numberField;

        private linetype linetypeField;

        private bool linetypeFieldSpecified;

        private string valueField;

        public glissando()
        {
            this.numberField = "1";
        }

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("1")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("line-type")]
        public linetype linetype
        {
            get
            {
                return this.linetypeField;
            }
            set
            {
                this.linetypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool linetypeSpecified
        {
            get
            {
                return this.linetypeFieldSpecified;
            }
            set
            {
                this.linetypeFieldSpecified = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "tuplet-dot")]
    public class tupletdot
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "tuplet-type")]
    public class tuplettype
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        private NoteTypeValue valueField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlText]
        public NoteTypeValue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "tuplet-number")]
    public class tupletnumber
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        private string valueField;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlTextAttribute(DataType = "nonNegativeInteger")]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    [XmlType(TypeName = "tuplet-portion")]
    public class tupletportion
    {

        private tupletnumber tupletnumberField;

        private tuplettype tuplettypeField;

        private tupletdot[] tupletdotField;

        
        [XmlElement("tuplet-number")]
        public tupletnumber tupletnumber
        {
            get
            {
                return this.tupletnumberField;
            }
            set
            {
                this.tupletnumberField = value;
            }
        }

        
        [XmlElement("tuplet-type")]
        public tuplettype tuplettype
        {
            get
            {
                return this.tuplettypeField;
            }
            set
            {
                this.tuplettypeField = value;
            }
        }

        
        [XmlElement("tuplet-dot")]
        public tupletdot[] tupletdot
        {
            get
            {
                return this.tupletdotField;
            }
            set
            {
                this.tupletdotField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class tuplet
    {

        private tupletportion tupletactualField;

        private tupletportion tupletnormalField;

        private startstop typeField;

        private string numberField;

        private YesNo bracketField;

        private bool bracketFieldSpecified;

        private showtuplet shownumberField;

        private bool shownumberFieldSpecified;

        private showtuplet showtypeField;

        private bool showtypeFieldSpecified;

        private lineshape lineshapeField;

        private bool lineshapeFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        
        [XmlElement("tuplet-actual")]
        public tupletportion tupletactual
        {
            get
            {
                return this.tupletactualField;
            }
            set
            {
                this.tupletactualField = value;
            }
        }

        
        [XmlElement("tuplet-normal")]
        public tupletportion tupletnormal
        {
            get
            {
                return this.tupletnormalField;
            }
            set
            {
                this.tupletnormalField = value;
            }
        }

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute]
        public YesNo bracket
        {
            get
            {
                return this.bracketField;
            }
            set
            {
                this.bracketField = value;
            }
        }

        
        [XmlIgnore]
        public bool bracketSpecified
        {
            get
            {
                return this.bracketFieldSpecified;
            }
            set
            {
                this.bracketFieldSpecified = value;
            }
        }

        
        [XmlAttribute("show-number")]
        public showtuplet shownumber
        {
            get
            {
                return this.shownumberField;
            }
            set
            {
                this.shownumberField = value;
            }
        }

        
        [XmlIgnore]
        public bool shownumberSpecified
        {
            get
            {
                return this.shownumberFieldSpecified;
            }
            set
            {
                this.shownumberFieldSpecified = value;
            }
        }

        
        [XmlAttribute("show-type")]
        public showtuplet showtype
        {
            get
            {
                return this.showtypeField;
            }
            set
            {
                this.showtypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool showtypeSpecified
        {
            get
            {
                return this.showtypeFieldSpecified;
            }
            set
            {
                this.showtypeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("line-shape")]
        public lineshape lineshape
        {
            get
            {
                return this.lineshapeField;
            }
            set
            {
                this.lineshapeField = value;
            }
        }

        
        [XmlIgnore]
        public bool lineshapeSpecified
        {
            get
            {
                return this.lineshapeFieldSpecified;
            }
            set
            {
                this.lineshapeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class slur
    {

        private StartStopContinue typeField;

        private string numberField;

        private linetype linetypeField;

        private bool linetypeFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private overunder orientationField;

        private bool orientationFieldSpecified;

        private decimal bezieroffsetField;

        private bool bezieroffsetFieldSpecified;

        private decimal bezieroffset2Field;

        private bool bezieroffset2FieldSpecified;

        private decimal bezierxField;

        private bool bezierxFieldSpecified;

        private decimal bezieryField;

        private bool bezieryFieldSpecified;

        private decimal bezierx2Field;

        private bool bezierx2FieldSpecified;

        private decimal beziery2Field;

        private bool beziery2FieldSpecified;

        private string colorField;

        public slur()
        {
            this.numberField = "1";
        }

        
        [XmlAttribute]
        public StartStopContinue type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        [DefaultValueAttribute("1")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("line-type")]
        public linetype linetype
        {
            get
            {
                return this.linetypeField;
            }
            set
            {
                this.linetypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool linetypeSpecified
        {
            get
            {
                return this.linetypeFieldSpecified;
            }
            set
            {
                this.linetypeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public overunder orientation
        {
            get
            {
                return this.orientationField;
            }
            set
            {
                this.orientationField = value;
            }
        }

        
        [XmlIgnore]
        public bool orientationSpecified
        {
            get
            {
                return this.orientationFieldSpecified;
            }
            set
            {
                this.orientationFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-offset")]
        public decimal bezieroffset
        {
            get
            {
                return this.bezieroffsetField;
            }
            set
            {
                this.bezieroffsetField = value;
            }
        }

        
        [XmlIgnore]
        public bool bezieroffsetSpecified
        {
            get
            {
                return this.bezieroffsetFieldSpecified;
            }
            set
            {
                this.bezieroffsetFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-offset2")]
        public decimal bezieroffset2
        {
            get
            {
                return this.bezieroffset2Field;
            }
            set
            {
                this.bezieroffset2Field = value;
            }
        }

        
        [XmlIgnore]
        public bool bezieroffset2Specified
        {
            get
            {
                return this.bezieroffset2FieldSpecified;
            }
            set
            {
                this.bezieroffset2FieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-x")]
        public decimal bezierx
        {
            get
            {
                return this.bezierxField;
            }
            set
            {
                this.bezierxField = value;
            }
        }

        
        [XmlIgnore]
        public bool bezierxSpecified
        {
            get
            {
                return this.bezierxFieldSpecified;
            }
            set
            {
                this.bezierxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-y")]
        public decimal beziery
        {
            get
            {
                return this.bezieryField;
            }
            set
            {
                this.bezieryField = value;
            }
        }

        
        [XmlIgnore]
        public bool bezierySpecified
        {
            get
            {
                return this.bezieryFieldSpecified;
            }
            set
            {
                this.bezieryFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-x2")]
        public decimal bezierx2
        {
            get
            {
                return this.bezierx2Field;
            }
            set
            {
                this.bezierx2Field = value;
            }
        }

        
        [XmlIgnore]
        public bool bezierx2Specified
        {
            get
            {
                return this.bezierx2FieldSpecified;
            }
            set
            {
                this.bezierx2FieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-y2")]
        public decimal beziery2
        {
            get
            {
                return this.beziery2Field;
            }
            set
            {
                this.beziery2Field = value;
            }
        }

        
        [XmlIgnore]
        public bool beziery2Specified
        {
            get
            {
                return this.beziery2FieldSpecified;
            }
            set
            {
                this.beziery2FieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "over-under")]
    public enum overunder
    {

        
        over,

        
        under,
    }

    
    
    [Serializable]
    
    
    public class tied
    {

        private startstop typeField;

        private string numberField;

        private linetype linetypeField;

        private bool linetypeFieldSpecified;

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private AboveBelow placementField;

        private bool placementFieldSpecified;

        private overunder orientationField;

        private bool orientationFieldSpecified;

        private decimal bezieroffsetField;

        private bool bezieroffsetFieldSpecified;

        private decimal bezieroffset2Field;

        private bool bezieroffset2FieldSpecified;

        private decimal bezierxField;

        private bool bezierxFieldSpecified;

        private decimal bezieryField;

        private bool bezieryFieldSpecified;

        private decimal bezierx2Field;

        private bool bezierx2FieldSpecified;

        private decimal beziery2Field;

        private bool beziery2FieldSpecified;

        private string colorField;

        
        [XmlAttribute]
        public startstop type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlAttribute(DataType = "positiveInteger")]
        public string number
        {
            get
            {
                return this.numberField;
            }
            set
            {
                this.numberField = value;
            }
        }

        
        [XmlAttribute("line-type")]
        public linetype linetype
        {
            get
            {
                return this.linetypeField;
            }
            set
            {
                this.linetypeField = value;
            }
        }

        
        [XmlIgnore]
        public bool linetypeSpecified
        {
            get
            {
                return this.linetypeFieldSpecified;
            }
            set
            {
                this.linetypeFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public AboveBelow placement
        {
            get
            {
                return this.placementField;
            }
            set
            {
                this.placementField = value;
            }
        }

        
        [XmlIgnore]
        public bool placementSpecified
        {
            get
            {
                return this.placementFieldSpecified;
            }
            set
            {
                this.placementFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public overunder orientation
        {
            get
            {
                return this.orientationField;
            }
            set
            {
                this.orientationField = value;
            }
        }

        
        [XmlIgnore]
        public bool orientationSpecified
        {
            get
            {
                return this.orientationFieldSpecified;
            }
            set
            {
                this.orientationFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-offset")]
        public decimal bezieroffset
        {
            get
            {
                return this.bezieroffsetField;
            }
            set
            {
                this.bezieroffsetField = value;
            }
        }

        
        [XmlIgnore]
        public bool bezieroffsetSpecified
        {
            get
            {
                return this.bezieroffsetFieldSpecified;
            }
            set
            {
                this.bezieroffsetFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-offset2")]
        public decimal bezieroffset2
        {
            get
            {
                return this.bezieroffset2Field;
            }
            set
            {
                this.bezieroffset2Field = value;
            }
        }

        
        [XmlIgnore]
        public bool bezieroffset2Specified
        {
            get
            {
                return this.bezieroffset2FieldSpecified;
            }
            set
            {
                this.bezieroffset2FieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-x")]
        public decimal bezierx
        {
            get
            {
                return this.bezierxField;
            }
            set
            {
                this.bezierxField = value;
            }
        }

        
        [XmlIgnore]
        public bool bezierxSpecified
        {
            get
            {
                return this.bezierxFieldSpecified;
            }
            set
            {
                this.bezierxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-y")]
        public decimal beziery
        {
            get
            {
                return this.bezieryField;
            }
            set
            {
                this.bezieryField = value;
            }
        }

        
        [XmlIgnore]
        public bool bezierySpecified
        {
            get
            {
                return this.bezieryFieldSpecified;
            }
            set
            {
                this.bezieryFieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-x2")]
        public decimal bezierx2
        {
            get
            {
                return this.bezierx2Field;
            }
            set
            {
                this.bezierx2Field = value;
            }
        }

        
        [XmlIgnore]
        public bool bezierx2Specified
        {
            get
            {
                return this.bezierx2FieldSpecified;
            }
            set
            {
                this.bezierx2FieldSpecified = value;
            }
        }

        
        [XmlAttribute("bezier-y2")]
        public decimal beziery2
        {
            get
            {
                return this.beziery2Field;
            }
            set
            {
                this.beziery2Field = value;
            }
        }

        
        [XmlIgnore]
        public bool beziery2Specified
        {
            get
            {
                return this.beziery2FieldSpecified;
            }
            set
            {
                this.beziery2FieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }
    }

    
    
    [Serializable]
    
    
    public class Notations
    {

        private FormattedText footnoteField;

        private Level levelField;

        private object[] itemsField;

        
        public FormattedText footnote
        {
            get
            {
                return this.footnoteField;
            }
            set
            {
                this.footnoteField = value;
            }
        }

        
        public Level level
        {
            get
            {
                return this.levelField;
            }
            set
            {
                this.levelField = value;
            }
        }

        
        [XmlElement("accidental-mark", typeof(accidentalmark))]
        [XmlElement("arpeggiate", typeof(arpeggiate))]
        [XmlElement("articulations", typeof(articulations))]
        [XmlElement("dynamics", typeof(dynamics))]
        [XmlElement("fermata", typeof(fermata))]
        [XmlElement("glissando", typeof(glissando))]
        [XmlElement("non-arpeggiate", typeof(nonarpeggiate))]
        [XmlElement("ornaments", typeof(ornaments))]
        [XmlElement("other-notation", typeof(othernotation))]
        [XmlElement("slide", typeof(slide))]
        [XmlElement("slur", typeof(slur))]
        [XmlElement("technical", typeof(technical))]
        [XmlElement("tied", typeof(tied))]
        [XmlElement("tuplet", typeof(tuplet))]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }
    }

    
    
    [Serializable]
    public class Beam
    {
        [XmlAttribute("number", DataType = "positiveInteger"), DefaultValue("1")] public string Number { get; set; }
        [XmlAttribute("repeater")] public YesNo Repeater { get; set; }
        [XmlIgnore] public bool RepeaterSpecified { get; set; }
        [XmlAttribute("fan")] public Fan Fan { get; set; }
        [XmlIgnore] public bool FanSpecified { get; set; }
        [XmlAttribute("color", DataType = "token")] public string Color { get; set; }
        [XmlText] public BeamValue Value { get; set; }

        public Beam()
        {
            Fan = Fan.None; 
            Color = "#000000"; 
            Value = BeamValue.Begin;
        }
    }

    
    
    [Serializable]
    [XmlType(TypeName = "fan")]
    public enum Fan
    {
        [XmlEnum("accel")] Accel,
        [XmlEnum("rit")] Rit,
        [XmlEnum("none")] None,
    }

    [Serializable]
    public class NoteHead
    {

        private YesNo filledField;

        private bool filledFieldSpecified;

        private YesNo parenthesesField;

        private bool parenthesesFieldSpecified;

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        private string colorField;

        private NoteHeadValue valueField;

        
        [XmlAttribute]
        public YesNo filled
        {
            get
            {
                return this.filledField;
            }
            set
            {
                this.filledField = value;
            }
        }

        
        [XmlIgnore]
        public bool filledSpecified
        {
            get
            {
                return this.filledFieldSpecified;
            }
            set
            {
                this.filledFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo parentheses
        {
            get
            {
                return this.parenthesesField;
            }
            set
            {
                this.parenthesesField = value;
            }
        }

        
        [XmlIgnore]
        public bool parenthesesSpecified
        {
            get
            {
                return this.parenthesesFieldSpecified;
            }
            set
            {
                this.parenthesesFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }

        
        [XmlAttribute(DataType = "token")]
        public string color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlText]
        public NoteHeadValue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "notehead-value")]
    public enum NoteHeadValue
    {

        
        slash,

        
        triangle,

        
        diamond,

        
        square,

        
        cross,

        
        x,

        
        [XmlEnum("circle-x")]
        circlex,

        
        [XmlEnum("inverted triangle")]
        invertedtriangle,

        
        [XmlEnum("arrow down")]
        arrowdown,

        
        [XmlEnum("arrow up")]
        arrowup,

        
        slashed,

        
        [XmlEnum("back slashed")]
        backslashed,

        
        normal,

        
        cluster,

        
        none,

        
        @do,

        
        re,

        
        mi,

        
        fa,

        
        so,

        
        la,

        
        ti,
    }
    
    [Serializable]
    public class Stem
    {

        private decimal defaultxField;

        private bool defaultxFieldSpecified;

        private decimal defaultyField;

        private bool defaultyFieldSpecified;

        private decimal relativexField;

        private bool relativexFieldSpecified;

        private decimal relativeyField;

        private bool relativeyFieldSpecified;

        private string colorField;

        private StemValue valueField;

        
        [XmlAttribute("default-x")]
        public decimal defaultx
        {
            get
            {
                return this.defaultxField;
            }
            set
            {
                this.defaultxField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultxSpecified
        {
            get
            {
                return this.defaultxFieldSpecified;
            }
            set
            {
                this.defaultxFieldSpecified = value;
            }
        }

        
        [XmlAttribute("default-y")]
        public decimal defaulty
        {
            get
            {
                return this.defaultyField;
            }
            set
            {
                this.defaultyField = value;
            }
        }

        
        [XmlIgnore]
        public bool defaultySpecified
        {
            get
            {
                return this.defaultyFieldSpecified;
            }
            set
            {
                this.defaultyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-x")]
        public decimal relativex
        {
            get
            {
                return this.relativexField;
            }
            set
            {
                this.relativexField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativexSpecified
        {
            get
            {
                return this.relativexFieldSpecified;
            }
            set
            {
                this.relativexFieldSpecified = value;
            }
        }

        
        [XmlAttribute("relative-y")]
        public decimal relativey
        {
            get
            {
                return this.relativeyField;
            }
            set
            {
                this.relativeyField = value;
            }
        }

        
        [XmlIgnore]
        public bool relativeySpecified
        {
            get
            {
                return this.relativeyFieldSpecified;
            }
            set
            {
                this.relativeyFieldSpecified = value;
            }
        }

        
        [XmlAttribute("color", DataType = "token")]
        public string Color
        {
            get
            {
                return this.colorField;
            }
            set
            {
                this.colorField = value;
            }
        }

        
        [XmlText]
        public StemValue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "stem-value")]
    public enum StemValue
    {
        [XmlEnum("down")] Down,
        [XmlEnum("up")] Up,
        [XmlEnum("double")] Double,
        [XmlEnum("none")] None,
    }

    [Serializable]
    public class Accidental
    {

        private YesNo cautionaryField;

        private bool cautionaryFieldSpecified;

        private YesNo editorialField;

        private bool editorialFieldSpecified;

        private YesNo parenthesesField;

        private bool parenthesesFieldSpecified;

        private YesNo bracketField;

        private bool bracketFieldSpecified;

        private SymbolSize sizeField;

        private bool sizeFieldSpecified;

        private accidentalvalue valueField;

        
        [XmlAttribute]
        public YesNo cautionary
        {
            get
            {
                return this.cautionaryField;
            }
            set
            {
                this.cautionaryField = value;
            }
        }

        
        [XmlIgnore]
        public bool cautionarySpecified
        {
            get
            {
                return this.cautionaryFieldSpecified;
            }
            set
            {
                this.cautionaryFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo editorial
        {
            get
            {
                return this.editorialField;
            }
            set
            {
                this.editorialField = value;
            }
        }

        
        [XmlIgnore]
        public bool editorialSpecified
        {
            get
            {
                return this.editorialFieldSpecified;
            }
            set
            {
                this.editorialFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo parentheses
        {
            get
            {
                return this.parenthesesField;
            }
            set
            {
                this.parenthesesField = value;
            }
        }

        
        [XmlIgnore]
        public bool parenthesesSpecified
        {
            get
            {
                return this.parenthesesFieldSpecified;
            }
            set
            {
                this.parenthesesFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public YesNo bracket
        {
            get
            {
                return this.bracketField;
            }
            set
            {
                this.bracketField = value;
            }
        }

        
        [XmlIgnore]
        public bool bracketSpecified
        {
            get
            {
                return this.bracketFieldSpecified;
            }
            set
            {
                this.bracketFieldSpecified = value;
            }
        }

        
        [XmlAttribute]
        public SymbolSize size
        {
            get
            {
                return this.sizeField;
            }
            set
            {
                this.sizeField = value;
            }
        }

        
        [XmlIgnore]
        public bool sizeSpecified
        {
            get
            {
                return this.sizeFieldSpecified;
            }
            set
            {
                this.sizeFieldSpecified = value;
            }
        }

        
        [XmlText]
        public accidentalvalue Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "note-type")]
    public class NoteType
    {
        [XmlAttribute("size")] public SymbolSize Size { get; set; }
        [XmlIgnore] public bool SizeSpecified { get; set; }
        [XmlText] public NoteTypeValue Value { get; set; }
    }

    [Serializable]
    public class Instrument
    {
        [XmlAttribute("id", DataType = "IDREF")]
        public string Id { get; set; }
    }

    [Serializable]
    public class Tie
    {
        [XmlAttribute("type")]
        public startstop Type { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "display-step-octave")]
    public class DisplayStepOctave
    {
        [XmlElement("display-step"), XmlIgnore]
        public Step DisplayStep { get; set; }


        [XmlElement("display-octave", DataType = "integer")]
        public string DisplayOctave { get; set; }
    }

    [Serializable]
    public class Pitch
    {
        [XmlElement("step")]
        public Step Step { get; set; }

        [XmlElement("alter")]
        public decimal Alter { get; set; }

        [XmlIgnore]
        public bool AlterationSpecified { get; set; }

        [XmlElement("octave", DataType = "integer")]
        public string Octave { get; set; }
    }

    [Serializable]
    public class Grace
    {
        [XmlAttribute("steal-time-previous")]
        public decimal StealTimePrevious { get; set; }

        [XmlIgnore]
        public bool StealTimePreviousSpecified { get; set; }

        [XmlAttribute("steal-time-following")]
        public decimal StealTimeFollowing { get; set; }

        [XmlIgnore]
        public bool StealTimeFollowingSpecified { get; set; }

        [XmlAttribute("make-time")]
        public decimal MakeTime { get; set; }

        [XmlIgnore]
        public bool MakeTimeSpecified { get; set; }

        [XmlAttribute("slash")]
        public YesNo Slash { get; set; }

        [XmlIgnore]
        public bool SlashSpecified { get; set; }
    }

    [Serializable]
    public class Note
    {
        [XmlElement("chord", typeof(empty))]
        [XmlElement("cue", typeof(empty))]
        [XmlElement("duration", typeof(decimal))]
        [XmlElement("grace", typeof(Grace))]
        [XmlElement("pitch", typeof(Pitch))]
        [XmlElement("rest", typeof(DisplayStepOctave))]
        [XmlElement("tie", typeof(Tie))]
        [XmlElement("unpitched", typeof(DisplayStepOctave))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items { get; set; }

        [XmlElement("ItemsElementName"), XmlIgnore]
        public ItemsChoiceType1[] ItemsElementName { get; set; }

        [XmlElement("instrument")] 
        public Instrument Instrument { get; set; }
        
        [XmlElement("footnote")]
        public FormattedText Footnote { get; set; }
        
        [XmlElement("level")]
        public Level Level { get; set; }
        
        [XmlElement("voice")]
        public string Voice { get; set; }
        
        [XmlElement("type")]
        public NoteType NoteType { get; set; }
        
        [XmlElement("dot")]
        public EmptyPlacement[] Dot { get; set; }

        [XmlElement("accidental")]
        public Accidental Accidental { get; set; }

        [XmlElement("time-modification")]
        public TimeModification TimeModification { get; set; }

        [XmlElement("stem")]
        public Stem Stem { get; set; }

        [XmlElement("notehead")]
        public NoteHead NoteHead { get; set; }

        [XmlElement("staff", DataType = "positiveInteger")]
        public string Staff { get; set; }
        
        [XmlElement("beam")]
        public Beam[] Beam { get; set; }
        
        [XmlElement("notations")]
        public Notations[] Notations { get; set; }
        
        [XmlElement("lyric")]
        public lyric[] Lyrics { get; set; }
        
        [XmlAttribute("default-x")]
        public decimal DefaultX { get; set; }
        
        [XmlIgnore]
        public bool DefaultXSpecified { get; set; }
        
        [XmlAttribute("default-y")]
        public decimal DefaultY { get; set; }
        
        [XmlIgnore]
        public bool DefaultYSpecified { get; set; }
        
        [XmlAttribute("relative-x")]
        public decimal RelativeX { get; set; }
        
        [XmlIgnore]
        public bool RelativeXSpecified { get; set; }
        
        [XmlAttribute("relative-y")]
        public decimal RelativeY { get; set; }
        
        [XmlIgnore]
        public bool RelativeYSpecified { get; set; }
        
        [XmlAttribute("font-family", DataType = "token")]
        public string FontFamily { get; set; }
        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle { get; set; }
        
        [XmlIgnore]
        public bool FontStyleSpecified { get; set; }
        
        [XmlAttribute("font-size")]
        public string FontSize { get; set; }
        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight { get; set; }
        
        [XmlIgnore]
        public bool FontWeightSpecified { get; set; }
        
        [XmlAttribute("color", DataType = "token")]
        public string Color { get; set; }
        
        [XmlAttribute("print-dot")]
        public YesNo PrintDot { get; set; }
        
        [XmlIgnore]
        public bool PrintDotSpecified { get; set; }
        
        [XmlAttribute("print-lyric")]
        public YesNo PrintLyric { get; set; }
        
        [XmlIgnore]
        public bool PrintLyricSpecified { get; set; }

        [XmlAttribute("dynamics")]
        public decimal Dynamics { get; set; }
        
        [XmlIgnore]
        public bool DynamicsSpecified { get; set; }
        
        [XmlAttribute("end-dynamics")]
        public decimal EndDynamics { get; set; }
        
        [XmlIgnore]
        public bool EndDynamicsSpecified { get; set; }

        [XmlAttribute("attack")]
        public decimal Attack { get; set; }
        
        [XmlIgnore]
        public bool AttackSpecified { get; set; }

        [XmlAttribute("release")]
        public decimal Release { get; set; }
        
        [XmlIgnore]
        public bool ReleaseSpecified { get; set; }
        
        [XmlAttribute("time-only", DataType = "token")]
        public string TimeOnly { get; set; }

        [XmlAttribute("pizzicato")]
        public YesNo Pizzicato { get; set; }
        
        [XmlIgnore]
        public bool PizzicatoSpecified { get; set; }

        [XmlIgnore]
        public Pitch Pitch
        {
            get 
            {
                return Items.Where(o => o.GetType() == typeof (Pitch)).Cast<Pitch>().First();
            }
            set 
            {
                if (Items == null) return;

                if (!IsRest)
                {
                    try
                    {
                        var oldPitch = Items.Where(o => o.GetType() == typeof(Pitch)).Cast<Pitch>().First();
                        var oldPitchIndex = Items.ToList().IndexOf(oldPitch);

                        Items[oldPitchIndex] = value;    
                    }
                    catch (NullReferenceException nulEx)
                    {
                        //Assume a new note creation
                        Items[0] = value;
                        ItemsElementName[0] = ItemsChoiceType1.Pitch;
                    }
                }
                else
                {
                    /*To change from a rest to a note
                     * the DisplayStepOctave must be 
                     * replaced by a Pitch object
                     */

                    var oldDisplayStepOctave = Items.Where(o => o.GetType() == typeof(DisplayStepOctave)).Cast<DisplayStepOctave>().First();
                    var oldDisplayStepOctaveIndex = Items.ToList().IndexOf(oldDisplayStepOctave);

                    var oldRestIndor = ItemsElementName.Where(o => o.GetType() == typeof(ItemsChoiceType1)).First();
                    var oldRestIndorIndex = ItemsElementName.ToList().IndexOf(oldRestIndor);

                    Items[oldDisplayStepOctaveIndex] = value;
                    ItemsElementName[oldRestIndorIndex] = ItemsChoiceType1.Pitch;
                }

                MidiValue = MidiMapping.MidiLookUp(value);
            }
        }

        [XmlIgnore] private int backingMidiValue = -1;

        [XmlIgnore]
        public int MidiValue
        {
            get { return (backingMidiValue != -1) ? backingMidiValue : MidiMapping.MidiLookUp(Pitch); }
            private set { backingMidiValue = value; }
        }

        [XmlIgnore]
        public bool IsRest
        {
            get { return ItemsElementName[0].ToString().Equals("Rest"); }
        }

        [XmlIgnore]
        public int Duration
        {
            get
            {
                return Convert.ToInt32(Items[1]);
            }
            set
            {
                Items[1] = Convert.ToDecimal(value);
                ItemsElementName[1] = ItemsChoiceType1.Duration;
            }
        }

        public Note() { } //Here for Xml (De)Serialization

        public Note(string staff, Pitch pitch, NoteType noteType, string voice = "1")
        {
            Items = new object[2]; /*At the very least a Pitch / Rest and duration*/
            ItemsElementName = new ItemsChoiceType1[2];
            Voice = voice;
            Staff = staff;
            Pitch = pitch;
            Duration = 1024; 
            NoteType = noteType;
            Stem = new Stem { Value = StemValue.Up };
            Instrument = new Instrument { Id = "P1-I1" };
        }

        public Note(Note note)
        {
            if (note.Items != null)
            {
                Items = new object[note.Items.Length];
                Array.Copy(note.Items, Items, note.Items.Length);
            }

            if (note.ItemsElementName != null)
            {
                ItemsElementName = new ItemsChoiceType1[note.ItemsElementName.Length];
                Array.Copy(note.ItemsElementName, ItemsElementName, note.ItemsElementName.Length);
            }

            if (note.Beam != null)
            {
                Beam = new Beam[note.Beam.Length];
                Array.Copy(note.Beam, Beam, note.Beam.Length);
            }

            if (note.Notations != null)
            {
                Notations = new Notations[note.Notations.Length];
                Array.Copy(note.Notations, Notations, note.Notations.Length);
            }

            if (note.Lyrics != null)
            {
                Lyrics = new lyric[note.Lyrics.Length];
                Array.Copy(note.Lyrics, Lyrics, note.Lyrics.Length); 
            }

            Instrument = note.Instrument;
            Footnote = note.Footnote;
            Level = note.Level;
            Voice = note.Voice;
            NoteType = note.NoteType;
            Dot = note.Dot;
            Accidental = note.Accidental;
            TimeModification = note.TimeModification;
            Stem = note.Stem;
            NoteHead = note.NoteHead;
            Staff = note.Staff;
            DefaultX = note.DefaultX;
            DefaultXSpecified = note.DefaultXSpecified;
            DefaultY = note.DefaultY;
            DefaultYSpecified = note.DefaultXSpecified;
            RelativeX = note.RelativeX;
            RelativeXSpecified = note.RelativeXSpecified;
            RelativeY = note.RelativeY;
            RelativeYSpecified = note.RelativeYSpecified;
            FontFamily = note.FontFamily;
            FontStyle = note.FontStyle;
            FontStyleSpecified = note.FontStyleSpecified;
            FontSize = note.FontSize;
            FontWeight = note.FontWeight;
            FontWeightSpecified = note.FontWeightSpecified;
            Color = note.Color;
            PrintDot = note.PrintDot;
            PrintDotSpecified = note.PrintDotSpecified;
            PrintLyric = note.PrintLyric;
            PrintLyricSpecified = note.PrintLyricSpecified;
            Dynamics = note.Dynamics;
            DynamicsSpecified = note.DynamicsSpecified;
            EndDynamics = note.EndDynamics;
            EndDynamicsSpecified = note.EndDynamicsSpecified;
            Attack = note.Attack;
            AttackSpecified = note.AttackSpecified;
            Release = note.Release;
            ReleaseSpecified = note.ReleaseSpecified;
            TimeOnly = note.TimeOnly;
            Pizzicato = note.Pizzicato;
            PizzicatoSpecified = note.PizzicatoSpecified;
        }
    }
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType1
    {
        [XmlEnum("chord")] Chord, 
        [XmlEnum("cue")] Cue,
        [XmlEnum("duration")] Duration,
        [XmlEnum("grace")] Grace,
        [XmlEnum("pitch")] Pitch,
        [XmlEnum("rest")] Rest,
        [XmlEnum("tie")] Tie,
        [XmlEnum("unpitched")] UnPitched,
    }
    
    [Serializable]
    [XmlType(TypeName = "midi-device")]
    public class MidiDevice
    {
        [XmlAttribute("port", DataType = "positiveInteger")]
        public string Port { get; set; }


        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "score-instrument")]
    public class ScoreInstrument
    {
        [XmlElement("instrument-name")]
        public string InstrumentName { get; set; }

        [XmlElement("instrument-abbreviation")]
        public string InstrumentAbbreviation { get; set; }

        [XmlElement("ensemble", typeof(string))]
        [XmlElement("solo", typeof(empty))]
        public object Item { get; set; }

        [XmlAttribute("id", DataType = "ID")]
        public string Id { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "part-name")]
    public class PartName
    {
        [XmlText]
        public string Value { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "score-part")]
    public class ScorePart
    {
        [XmlElement("identification")]
        public Identification Identification { get; set; }

        [XmlElement("part-name")]
        public PartName PartName { get; set; }

        [XmlElement("part-name-display")]
        public namedisplay PartNameDisplay { get; set; }

        [XmlElement("part-abbreviation")]
        public PartName PartAbbreviation { get; set; }

        [XmlElement("part-abbreviation-display")]
        public namedisplay PartAbbreviationDisplay { get; set; }

        [XmlElement("group")]
        public string[] Group { get; set; }

        [XmlElement("score-instrument")]
        public ScoreInstrument[] ScoreInstrument { get; set; }

        [XmlElement("midi-device")]
        public MidiDevice MidiDevice { get; set; }

        [XmlElement("midi-instrument")]
        public midiinstrument[] MidiInstrument { get; set; }

        [XmlAttribute("id", DataType = "ID")]
        public string Id { get; set; }
    }

    [Serializable]
    public class Identification
    {
        [XmlElement("creator")]
        public TypedText[] Creator { get; set; }


        [XmlElement("rights")]
        public TypedText[] Rights { get; set; }

        [XmlElement("encoding")]
        public Encoding Encoding { get; set; }

        [XmlElement("source")]
        public string Source { get; set; }

        [XmlElement("relation")]
        public TypedText[] Relation { get; set; }

        [XmlArrayItem("miscellaneou", IsNullable = false)]
        public MiscellaneousField[] miscellaneous { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "typed-text")]
    public class TypedText
    {
        [XmlAttribute("type", DataType = "token")]
        public string Type { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    public class Encoding
    {
        [XmlElement("encoder", typeof(TypedText))]
        [XmlElement("encoding-date", typeof(DateTime), DataType = "date")]
        [XmlElement("encoding-description", typeof(string))]
        [XmlElement("software", typeof(string))]
        [XmlElement("supports", typeof(Supports))]
        [XmlChoiceIdentifierAttribute("ItemsElementName")]
        public object[] Items { get; set; }

        [XmlElement("ItemsElementName"), XmlIgnore]
        public ItemsChoiceType[] ItemsElementName { get; set; }
    }

    [Serializable]
    public class Supports
    {
        [XmlAttribute("type")]
        public YesNo Type { get; set; }

        [XmlAttribute("element", DataType = "NMTOKEN")]
        public string Element { get; set; }

        [XmlAttribute("attribute", DataType = "NMTOKEN")]
        public string Attribute { get; set; }

        [XmlAttribute("value", DataType = "token")]
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(IncludeInSchema = false)]
    public enum ItemsChoiceType
    {
        [XmlEnum("encoder")] Encoder,
        [XmlEnum("encoding-date")] Encodingdate,
        [XmlEnum("encoding-description")] Encodingdescription,
        [XmlEnum("software")] Software,
        [XmlEnum("supports")] Supports,
    }
    
    [Serializable]
    [XmlType(TypeName = "miscellaneous-field")]
    public class MiscellaneousField
    {
        [XmlAttribute("name", DataType = "token")] 
        public string Name { get; set; }

        [XmlText] 
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "group-barline")]
    public class GroupBarline
    {
        [XmlAttribute("color", DataType = "token")]
        public string Color { get; set; }

        [XmlText]
        public GroupBarlineValue Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "group-barline-value")]
    public enum GroupBarlineValue
    {
        [XmlEnum("yes")] Yes,
        [XmlEnum("no")] No,
        [XmlEnum("Mensurstrich")] Mensurstrich,
    }

    [Serializable]
    [XmlType(TypeName = "group-symbol")]
    public class GroupSymbol
    {
        [XmlAttribute("default-x")]
        public decimal DefaultX { get; set; }

        [XmlIgnore]
        public bool DefaultXSpecified { get; set; }

        [XmlAttribute("default-y")]
        public decimal DefaultY { get; set; }

        [XmlIgnore]
        public bool DefaultYSpecified { get; set; }

        [XmlAttribute("relative-x")]
        public decimal RelativeX { get; set; }

        [XmlIgnore]
        public bool RelativeXSpecified { get; set; }

        [XmlAttribute("relative-y")]
        public decimal RelativeY { get; set; }

        [XmlIgnore]
        public bool RelativeYSpecified { get; set; }

        [XmlAttribute("color", DataType = "token")]
        public string Color { get; set; }

        [XmlText]
        public groupsymbolvalue Value { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "group-name")]
    public class GroupName
    {
        [XmlText]
        public string Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "part-group")]
    public class PartGroup
    {
        [XmlElement("group-name")]
        public GroupName GroupName { get; set; }

        [XmlElement("group-name-display")]
        public namedisplay GroupNameDisplay { get; set; }

        [XmlElement("group-abbreviation")]
        public GroupName GroupAbbreviation { get; set; }

        [XmlElement("group-abbreviation-display")]
        public namedisplay GroupAbbreviationDisplay { get; set; }

        [XmlElement("group-symbol")]
        public GroupSymbol GroupSymbol { get; set; }

        [XmlElement("group-barline")]
        public GroupBarline GroupBarline { get; set; }

        [XmlElement("group-time")]
        public empty GroupTime { get; set; }

        [XmlElement("footnote")]
        public FormattedText Footnote { get; set; }

        [XmlElement("level")]
        public Level Level { get; set; }

        [XmlAttribute("type")]
        public startstop Type { get; set; }

        [XmlAttribute("number", DataType = "token"), DefaultValue("1")]
        public string Number { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "part-list")]
    public class PartList
    {
        [XmlElement("part-group", Order = 0)]
        public PartGroup[] PartGroups { get; set; }

        [XmlElement("score-part", Order = 1)]
        public ScorePart ScorePart { get; set; }

        [XmlElement("part-group", typeof(PartGroup), Order = 2)]
        [XmlElement("score-part", typeof(ScorePart), Order = 2)]
        public object[] Items { get; set; }
    }

    [Serializable]
    public class Bookmark
    {
        [XmlAttribute("id", DataType = "ID")]
        public string Id { get; set; }

        [XmlAttribute("name", DataType = "token")]
        public string Name { get; set; }

        [XmlAttribute("element", DataType = "NMTOKEN")]
        public string Element { get; set; }

        [XmlAttribute("position", DataType = "positiveInteger")]
        public string Position { get; set; }
    }

    [Serializable]
    public class Link
    {
        [XmlAttribute("href", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "anyURI")]
        public string Href { get; set; }

        [XmlAttribute("type", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink"), DefaultValue(OpusType.Simple)]
        public OpusType Type { get; set; }

        [XmlIgnore]
        public bool TypeSpecified { get; set; }

        [XmlAttribute("role", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "token")]
        public string Role { get; set; }

        [XmlAttribute("title", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "token")]
        public string Title { get; set; }

        [XmlAttribute("show", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink"), DefaultValue(OpusShow.Replace)]
        public OpusShow Show { get; set; }

        [XmlAttribute("actuate", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink"), DefaultValue(OpusActuate.OnRequest)]
        public OpusActuate Actuate { get; set; }

        [XmlAttribute("name", DataType = "token")]
        public string Name { get; set; }

        [XmlAttribute("element", DataType = "NMTOKEN")]
        public string Element { get; set; }

        [XmlAttribute("position", DataType = "positiveInteger")]
        public string Position { get; set; }

        [XmlAttribute("default-x")]
        public decimal DefaultX { get; set; }

        [XmlIgnore]
        public bool DefaultXSpecified { get; set; }

        [XmlAttribute("default-y")]
        public decimal DefaultY { get; set; }

        [XmlIgnore]
        public bool DefaultYSpecified { get; set; }

        [XmlAttribute("relative-x")]
        public decimal RelativeX { get; set; }

        [XmlIgnore]
        public bool RelativeXSpecified { get; set; }

        [XmlAttribute("relative-y")]
        public decimal RelativeY { get; set; }

        [XmlIgnore]
        public bool RelativeYSpecified { get; set; }
    }

    [Serializable]
    public class Credit
    {
        [XmlElement("link", Order = 0)]
        public Link[] Links { get; set; }

        [XmlElement("bookmark", Order = 1)]
        public Bookmark[] Bookmarks { get; set; }

        [XmlElement("bookmark", typeof(Bookmark), Order = 2)]
        [XmlElement("credit-image", typeof(image), Order = 2)]
        [XmlElement("credit-words", typeof(FormattedText), Order = 2)]
        [XmlElement("link", typeof(Link), Order = 2)]
        public object[] Items { get; set; }

        [XmlAttribute("page", DataType = "positiveInteger")]
        public string Page { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "lyric-language")]
    public class LyricLanguage
    {
        [XmlAttribute("number", DataType = "NMTOKEN")]
        public string Number { get; set; }

        [XmlAttribute("name", DataType = "token")]
        public string Name { get; set; }

        [XmlAttribute("lang", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string Language { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "lyric-font")]
    public class LyricFont
    {
        [XmlAttribute(DataType = "NMTOKEN")]
        public string number { get; set; }


        [XmlAttribute(DataType = "token")]
        public string name { get; set; }


        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily { get; set; }


        [XmlAttribute("font-style")]
        public FontStyle FontStyle { get; set; }


        [XmlIgnore]
        public bool fontstyleSpecified { get; set; }


        [XmlAttribute("font-size")]
        public string fontsize { get; set; }


        [XmlAttribute("font-weight")]
        public FontWeight FontWeight { get; set; }


        [XmlIgnore]
        public bool fontweightSpecified { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "empty-font")]
    public class EmptyFont
    {

        private string fontfamilyField;

        private FontStyle fontStyleField;

        private bool fontstyleFieldSpecified;

        private string fontsizeField;

        private FontWeight fontWeightField;

        private bool fontweightFieldSpecified;

        
        [XmlAttribute("font-family", DataType = "token")]
        public string fontfamily
        {
            get
            {
                return this.fontfamilyField;
            }
            set
            {
                this.fontfamilyField = value;
            }
        }

        
        [XmlAttribute("font-style")]
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyleField;
            }
            set
            {
                this.fontStyleField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontstyleSpecified
        {
            get
            {
                return this.fontstyleFieldSpecified;
            }
            set
            {
                this.fontstyleFieldSpecified = value;
            }
        }

        
        [XmlAttribute("font-size")]
        public string fontsize
        {
            get
            {
                return this.fontsizeField;
            }
            set
            {
                this.fontsizeField = value;
            }
        }

        
        [XmlAttribute("font-weight")]
        public FontWeight FontWeight
        {
            get
            {
                return this.fontWeightField;
            }
            set
            {
                this.fontWeightField = value;
            }
        }

        
        [XmlIgnore]
        public bool fontweightSpecified
        {
            get
            {
                return this.fontweightFieldSpecified;
            }
            set
            {
                this.fontweightFieldSpecified = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "other-appearance")]
    public class OtherAppearance
    {

        private string typeField;

        private string valueField;

        
        [XmlAttribute(DataType = "token")]
        public string type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlText]
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    [Serializable]
    [XmlType(TypeName = "note-size")]
    public class NoteSize
    {

        private NoteSizeType typeField;

        private decimal valueField;

        
        [XmlAttribute]
        public NoteSizeType type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        
        [XmlText]
        public decimal Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }
    
    [Serializable]
    [XmlType(TypeName = "note-size-type")]
    public enum NoteSizeType
    {

        
        cue,

        
        grace,

        
        large,
    }
    
    [Serializable]
    [XmlType(TypeName = "line-width")]
    public class LineWidth
    {
        [XmlAttribute("type", DataType = "token")]
        public string Type { get; set; }


        [XmlText]
        public decimal Value { get; set; }
    }
    
    [Serializable]
    [XmlType(TypeName = "appearance")]
    public class Appearance
    {
        [XmlElement("line-width")]
        public LineWidth[] LineWidth { get; set; }

        [XmlElement("note-size")]
        public NoteSize[] NoteSize { get; set; }

        [XmlElement("other-appearance")]
        public OtherAppearance[] OtherAppearance { get; set; }
    }
    
    /*Don't know if it should be XmlType or XmlAttributeType*/
    [Serializable]
    [XmlType(TypeName = "scaling")]
    public class Scaling
    {
        /*Don't know if it should be XmlElement or XmlAttribute*/
        [XmlElement("millimeters")]
        public decimal Millimeters { get; set; }

        /*Don't know if it should be XmlElement or XmlAttribute*/
        [XmlElement("tenths")]
        public decimal Tenths { get; set; }
    }

    [Serializable]
    [XmlType(TypeName = "defaults")]
    public class Defaults
    {
        [XmlElement("scaling")]
        public Scaling Scaling { get; set; }

        [XmlElement("page-layout")]
        public pagelayout PageLayout { get; set; }

        [XmlElement("system-layout")]
        public systemlayout SystemLayout { get; set; }

        [XmlElement("staff-layout")]
        public stafflayout[] StaffLayouts { get; set; }

        [XmlElement("appearance")]
        public Appearance Appearance { get; set; }

        [XmlElement("music-font")]
        public EmptyFont MusicFont { get; set; }

        [XmlElement("word-font")]
        public EmptyFont WordFont { get; set; }

        [XmlElement("lyric-font")]
        public LyricFont[] LyricFonts { get; set; }

        [XmlElement("lyric-language")]
        public LyricLanguage[] LyricLanguages { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    public class ScorePartwisePart
    {
        [XmlElement("measure")]
        public ScorePartwisePartMeasure[] Measures { get; set; }

        [XmlAttribute("id", DataType = "IDREF")]
        public string Id { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    public class ScorePartwisePartMeasure
    {
        [XmlElement("attributes", typeof(Attributes))]
        [XmlElement("backup", typeof(backup))]
        [XmlElement("barline", typeof(BarLine))]
        [XmlElement("bookmark", typeof(Bookmark))]
        [XmlElement("direction", typeof(Direction))]
        [XmlElement("figured-bass", typeof(FiguredBass))]
        [XmlElement("forward", typeof(forward))]
        [XmlElement("grouping", typeof(Grouping))]
        [XmlElement("harmony", typeof(harmony))]
        [XmlElement("link", typeof(Link))]
        [XmlElement("note", typeof(Note))]
        [XmlElement("print", typeof(print))]
        [XmlElement("sound", typeof(Sound))]
        public object[] Items { get; set; }

        [XmlAttribute("number", DataType = "token")]
        public string Number { get; set; }

        [XmlAttribute("implicit")]
        public YesNo Implicit { get; set; }

        [XmlIgnore]
        public bool ImplicitSpecified { get; set; }

        [XmlAttribute("non-controlling")]
        public YesNo NonControlling { get; set; }

        [XmlIgnore]
        public bool NonControllingSpecified { get; set; }

        [XmlAttribute("width")]
        public decimal Width { get; set; }

        [XmlIgnore]
        public bool WidthSpecified { get; set; }

        [XmlIgnore]
        public List<Note> Notes
        {
            get { return (Items != null) ? Items.Where(o => o.GetType() == typeof (Note)).Cast<Note>().ToList() : null; }
        }

        [XmlIgnore]
        public Direction Direction
        {
            get
            {
                if (Items[0].GetType() == typeof(Direction)) return (Direction)Items[0];
                
                Direction = new Direction();

                return Direction;
            }
            set
            {
                if (Items[0].GetType() == typeof (Direction))
                {
                    Items[0] = value;
                    return;
                }

                var array = new List<object>() { value };
                array.AddRange(Items);

                Items = array.ToArray();
            }
        }


        public ScorePartwisePartMeasure() { }

        public ScorePartwisePartMeasure(ScorePartwisePartMeasure measure)
        {
            if (measure.Items != null)
            {
                Items = new object[measure.Items.Length];
                Array.Copy(measure.Items, Items, measure.Items.Length);
            }

            Number = measure.Number;
            Implicit = measure.Implicit;
            ImplicitSpecified = measure.ImplicitSpecified;
            NonControlling = measure.NonControlling;
            NonControllingSpecified = measure.NonControllingSpecified;
            Width = measure.Width;
            WidthSpecified = measure.WidthSpecified;
        }

        public void AddNote(Note note, string staffId)
        {
            var iStaffId = Convert.ToInt32(staffId);

            if (iStaffId == 1) return;

            var backupIndex = -1;

            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].GetType() != typeof (backup)) continue;

                backupIndex = i;
                break;
            }

            var measureItems = Items.ToList();

            for (int i = backupIndex + 1; i < Items.Length; i++)
                measureItems.Remove(Items[i]);

            measureItems.Add(note);
            Items = measureItems.ToArray();
        }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    [XmlRoot("score-timewise", Namespace = "", IsNullable = false)]
    public class ScoreTimewise
    {
        [XmlElement("movement-number")]
        public Work Work { get; set; }

        [XmlElement("movement-number")]
        public string MovementNumber { get; set; }

        [XmlElement("movement-title")]
        public string MovementTitle { get; set; }

        [XmlElement("identification")]
        public Identification Identification { get; set; }

        [XmlElement("defaults")]
        public Defaults Defaults { get; set; }

        [XmlElement("credit")]
        public Credit[] Credits { get; set; }
        
        [XmlElement("part-list")]
        public PartList PartList { get; set; }
        
        [XmlElement("measure")]
        public ScoreTimewiseMeasure[] Measures { get; set; }

        [XmlAttribute("version", DataType = "token"), DefaultValue("1.0")]
        public string Version { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    public class ScoreTimewiseMeasure
    {
        [XmlElement("part")]
        public ScoreTimewiseMeasurePart[] part { get; set; }

        [XmlAttribute(DataType = "token")]
        public string number { get; set; }

        [XmlAttribute]
        public YesNo @implicit { get; set; }

        [XmlIgnore]
        public bool implicitSpecified { get; set; }

        [XmlAttribute("non-controlling")]
        public YesNo noncontrolling { get; set; }

        [XmlIgnore]
        public bool noncontrollingSpecified { get; set; }

        [XmlAttribute]
        public decimal width { get; set; }

        [XmlIgnore]
        public bool widthSpecified { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true)]
    public class ScoreTimewiseMeasurePart
    {
        [XmlElement("attributes", typeof(Attributes))]
        [XmlElement("backup", typeof(backup))]
        [XmlElement("barline", typeof(BarLine))]
        [XmlElement("bookmark", typeof(Bookmark))]
        [XmlElement("direction", typeof(Direction))]
        [XmlElement("figured-bass", typeof(FiguredBass))]
        [XmlElement("forward", typeof(forward))]
        [XmlElement("grouping", typeof(Grouping))]
        [XmlElement("harmony", typeof(harmony))]
        [XmlElement("link", typeof(Link))]
        [XmlElement("note", typeof(Note))]
        [XmlElement("print", typeof(print))]
        [XmlElement("sound", typeof(Sound))]
        public object[] Items { get; set; }

        [XmlAttribute("id", DataType = "IDREF")]
        public string Id { get; set; }
    }

}
