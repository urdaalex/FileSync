# FileSync
A multi-threaded command line file syncing tool. Written in C# for Windows.

##Usage
```
FileSync.exe /s=<source> /d=<destination> [/l=<logfile>] [-v]
```

##How it works
Consider a directory you wish to sync with the following makeup:
###Before
```
Folder1
  Folder2
    A.txt
  D.txt
  E.txt
Folder3
  [Empty]
```

Calling: ``` FileSync.exe /s="../Folder1" /d="../Folder3" ``` will search for all differences between files and folders in Folder1 and Folder4 and transfer the appropriate files.

###After
```
Folder1
  Folder2
    A.txt
  D.txt
  E.txt
Folder3
  Folder2
    A.txt
  D.txt
  E.txt
```


A folder is created on the destination side if it exists in source side. A file is tranfered from the source side to the destination side if: the file does not exist in the destination side, or the modification time between the two files is greater than 1 second. Files and folders are never deleted from the destination side. 

Traversal of the source side files and folders is accomplished asynchronously using a thread pool. 

##Notes
- No support for symlink folders

##Future Work
- Create a GUI
- Asynchronous logging support
- Perform testing on different threading options
- Error-handling
