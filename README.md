# MP3catSharp  

This repo is C# implementation of [mp3cat](https://github.com/dmulholl/mp3cat).  

It's a simple command line utility for merging MP3 files without re-encoding.  

### Use in command line  

If you want to use it in the command line, you can download pre-compiled binary here [mp3cat](https://github.com/dmulholl/mp3cat).  

```
> mp3cat one.mp3 two.mp3 output.mp3
```

### Use in C#    

If you want to use it in C#, please follow the steps below.  

---  

### Nuget install  

```
PM> Install-Package MP3catSharp
```

### Example  

```C#
MP3cat.merge(outpath, inpaths, tagpath, force, quiet);
```

### Parameters  

* **outpath:** str, required  
　Output filepath.  

* **inpaths:** str[], required  
　List of files to merge.  

* **tagpath:** str, optional, default: ""  
　Copy the ID3v2 tag from the n-th input file.  

* **force:** bool, optional, default: false    
　Overwrite an existing output file.  

* **quiet:** bool, optional, default: false    
　Quiet mode. only output error messages.  

---  

### Re-compile  

Then if you want to use C# to re-compile the [mp3cat.go](https://github.com/dmulholl/mp3cat), you can refer [here](https://github.com/fysh711426/MP3catSharp/blob/master/mp3cat/Program.cs).  