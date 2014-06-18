Hi!

The primary reason for creating this library was for personal choice. I'm currently working on a project for algorithmic music composition and 
needed a standard for converting between algorithmic representations and standard music notation for importing into a program such as Sibelius 7.

Test Files:
The library comes bundled with a musicXMLtester project which deserialises a file called "cantus firmus.xml" and produces a "output.xml" file. 
The program, before serialising the output file, shows a list of all the notes contained within the file separated by part IDs


The major methods of the library:

/// <summary>
///     Deserializes a Music XML file
/// </summary>
/// <param name="filepath">The path to the xml file</param>
/// <returns>An instance of<see cref="ScorePartwise" /></returns>
public static ScorePartwise Deserialize(string filepath)

/// <summary>
///     Serializes a <see cref="ScorePartwise" /> to an XML file
/// </summary>
/// <param name="filepath">The path to the file</param>
/// <param name="scorepartwise">the score to be serialized</param>
public static void Serialize(string filepath, ScorePartwise scorepartwise)

The class MIDI-mapping serves as a support class not only for musicXml as a namespace, but for my initial program as well.
It has basic functionalities for determining if an interval is Major, Minor or perfect as well as assisting in the conversion 
of mapping notes to their MIDI equivalent.

TODOs: 

-> Renaming of classes to conform to .net naming conventions

-> Commenting and a lot of commenting (I haven't had time to comment the majority of the classes)

-> Unit testing. As stated this is a library for myself primarily, and thus I haven't had time to actually unit test the majority of the functions

-> The separation of classes into individual files should preferably occur only once the classes have their names conforming to the correct naming conventions

