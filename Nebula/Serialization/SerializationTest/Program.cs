// See https://aka.ms/new-console-template for more information
using SerializationTest;
using System.Diagnostics;

Console.WriteLine("Hello, World!");
//Serialization.SerializationUtil.Deserialize<SerializableObjectA>("./SerializationTest/SerializabaleObjectA");
var serializableObjectB = Serialization.SerializationUtil.Deserialize<SerializableObjectB>("./SerializationTest/SerializabaleObjectB", false);
Console.WriteLine(serializableObjectB.ToString());