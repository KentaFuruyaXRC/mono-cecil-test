using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Compilation;

[InitializeOnLoad]
public class CecilAssemblyPostProcessor
{
	static CecilAssemblyPostProcessor()
	{
		CompilationPipeline.assemblyCompilationFinished += AssemblyCompilationFinished;
		// CompilationPipeline.compilationStarted += OnCompilationStarted;
		// CompilationPipeline.compilationFinished += OnCompilationFinished;
	}

	private static void AssemblyCompilationFinished(string assemblyPath, CompilerMessage[] arg2)
	{
		EditorApplication.LockReloadAssemblies();

		try
		{
			HashSet<string> assemblySearchDirectories = new HashSet<string>();

			UnityEditor.Compilation.Assembly unityAssembly = CompilationPipeline.GetAssemblies().First(a => a.outputPath == assemblyPath);

			assemblySearchDirectories.Add(Path.GetDirectoryName(unityAssembly.outputPath));
			foreach (var assembly in unityAssembly.assemblyReferences)
			{
				assemblySearchDirectories.Add(Path.GetDirectoryName(assembly.outputPath));
			}

			DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();

			foreach (var searchDirectory in assemblySearchDirectories)
			{
				assemblyResolver.AddSearchDirectory(searchDirectory);
			}

			ReaderParameters readerParameters = new ReaderParameters();
			readerParameters.AssemblyResolver = assemblyResolver;

			WriterParameters writerParameters = new WriterParameters();

			String mdbPath = assemblyPath + ".mdb";
			String pdbPath = assemblyPath.Substring(0, assemblyPath.Length - 3) + "pdb";

			if (File.Exists(pdbPath))
			{
				readerParameters.ReadSymbols = true;
				readerParameters.SymbolReaderProvider = new Mono.Cecil.Pdb.PdbReaderProvider();
				writerParameters.WriteSymbols = true;
				writerParameters.SymbolWriterProvider = new Mono.Cecil.Mdb.MdbWriterProvider();
			}
			else if (File.Exists(mdbPath))
			{
				readerParameters.ReadSymbols = true;
				readerParameters.SymbolReaderProvider = new Mono.Cecil.Mdb.MdbReaderProvider();
				writerParameters.WriteSymbols = true;
				writerParameters.SymbolWriterProvider = new Mono.Cecil.Mdb.MdbWriterProvider();
			}
			else
			{
				readerParameters.ReadSymbols = false;
				readerParameters.SymbolReaderProvider = null;
				writerParameters.WriteSymbols = false;
				writerParameters.SymbolWriterProvider = null;
			}

			Debug.Log(assemblyPath + " + " + readerParameters);
			AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);

			if (ProcessAssembly(assemblyDefinition))
			{
				//IL書き換え適用
				assemblyDefinition.Write(assemblyPath, writerParameters);
				Debug.Log("Done writing " + assemblyPath);
			}
			else
			{
				Debug.Log(Path.GetFileName(assemblyPath) + " didn't need to be processed");
			}
			EditorApplication.UnlockReloadAssemblies();
		}
		catch (Exception e)
		{
			EditorApplication.UnlockReloadAssemblies();
			Debug.LogException(e);
		}
	}

	private static bool ProcessAssembly( AssemblyDefinition assemblyDefinition )
    {
        bool wasProcessed = false;
		Debug.Log("ProcessAssembly " + assemblyDefinition.FullName);

		foreach ( ModuleDefinition moduleDefinition in assemblyDefinition.Modules )
        {
            foreach( TypeDefinition typeDefinition in moduleDefinition.Types )
            {
                foreach( MethodDefinition methodDefinition in typeDefinition.Methods )
                {
                    CustomAttribute logAttribute = null;

                    foreach( CustomAttribute customAttribute in methodDefinition.CustomAttributes )
                    {
                        if( customAttribute.AttributeType.FullName == "LogAttribute" )
                        {
                            MethodReference logMethodReference = moduleDefinition.ImportReference( typeof( Debug ).GetMethod( "Log", new Type[] { typeof( object ) } ) );

                            ILProcessor ilProcessor = methodDefinition.Body.GetILProcessor();

                            Instruction first = methodDefinition.Body.Instructions[0];
                            ilProcessor.InsertBefore( first, Instruction.Create( OpCodes.Ldstr, "Enter " + typeDefinition.FullName + "." + methodDefinition.Name ) );
                            ilProcessor.InsertBefore( first, Instruction.Create( OpCodes.Call, logMethodReference ) );

                            Instruction last = methodDefinition.Body.Instructions[methodDefinition.Body.Instructions.Count - 1];
                            ilProcessor.InsertBefore( last, Instruction.Create( OpCodes.Ldstr, "Exit " + typeDefinition.FullName + "." + methodDefinition.Name ) );
                            ilProcessor.InsertBefore( last, Instruction.Create( OpCodes.Call, logMethodReference ) );

                            wasProcessed = true;
                            logAttribute = customAttribute;
                            break;
                        }
                    }

                    // Remove the attribute so it won't be processed again
                    if( logAttribute != null )
                    {
                        methodDefinition.CustomAttributes.Remove( logAttribute );
                    }
                }
            }
        }
        
        return wasProcessed;
    }
}
