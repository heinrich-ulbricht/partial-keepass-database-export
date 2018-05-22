# Partial KeePass Database Export
Export tagged entries to a new password database every time you save. The new database can use a different password.

# What does it do?
Every time you save your database a new database will be created and passwords with certain tags will be copied over (in KeePass you can mark any password entry with tags). No user interaction is required, the whole process runs in the background.

Without further configuration the new file is created in the same folder as your database file. If your database file is *passwords.kdbx* the new file will be named *passwords.partial-!readonly!.kdbx*. This file will be overwritten __every time__ you save the original database, so don't make any changes to the copy. (Note: as of v1.1.0 you can change output path and file name. The same reasoning applies, make sure to not overwrite existing files.)

The new database can be protected with a password different from the original database's one. Key files and Windows account are not supported.

# What's the use case?
You have a giant password database in your secure bunker at home and don't want this database to ever leave your home. But sometimes you need *a few* passwords on the go. What now?

Use this plugin. Mark the passwords you need on the go with a certain tag and take the partial database exported by the plugin with you (or upload it to the cloud). If somebody steals this password database he only has a few passwords, not all. And since the partial database uses a different password an attacker obtaining it cannot open the source database as well.

# Installation
Download the latest [release](https://github.com/heinrich-ulbricht/partial-keepass-database-export/releases/latest). 

Then:
1. unzip the file(s)
1. create a new sub-folder of the KeePass plugins folder, if it doesn't exist (e.g. *KeePass\Plugins\PartialKeePassDatabaseExport* - note: you have to create the sub-folder *PartialKeePassDatabaseExport* when installing the plugin for the first time)
1. copy the files to this sub-folder

Plugin files that **must** be copied to *KeePass\Plugins\PartialKeePassDatabaseExport* are:
* PartialKeePassDatabaseExport.dll

Now configure the plugin.

# Configuration
Create a special password entry with the title *PartialExportConfig* in the root group. The plugin will search for this entry.

The password of this entry is the password for the exported database.

The tags of this entry are the tags the plugin looks for. Passwords having those tags will be exported. Multiple tags are supported, they are delimited by comma (,).

# Advanced Configuration
Note: this section assumes you created the special password entry *PartialExportConfig* as described above.

## Set output file path and name (v1.1.0)
You can use the *URL* field of the *PartialExportConfig* to specify file name and/or path of the exported database file.

Supported values are:
* absolute pathes (*C:\OneDrive\PartialExport.kdbx*)
* relative pathes (*Export/PartialExport.kdbx*, *../PartialExport.kdbx*) - they will be resolved relative to the current database
* no path (*PartialExport.kdbx*) - will export to the directory of the current database

The *kdbx* file extension will be added automatically if missing.

## Clear *otp* string field
For the *PartialExportConfig* entry create a string field with name *clearotp* to NOT export the *otp* string field of entries to the new database. The *otp* field is used by the [KeeOtp](https://keepass.info/plugins.html#keeotp) plugin to store the secret key for one-time password generation. If you use other devices to generate one-time passwords you might want to exclude those keys from the exported database to not expose them to attackers.

Note: the value of the *clearotp* field doesn't matter and can be empty.

# Limitations

* **the group/folder structure is not copied** - the exported database will have the root folder containing all exported passwords
* export to only one file
* the new database can only be protected by password, not key files or a Windows account

# Partial KeePass Database Export vs KeePassSubsetExport

There is another plugin [KeePassSubsetExport](https://github.com/lukeIam/KeePassSubsetExport) out there, apparently developed in parallel, that does at least the same as my plugin. Have a look at the comparison [here](https://github.com/lukeIam/KeePassSubsetExport#keepasssubsetexport-vs-partial-keepass-database-export).

So here are the advantages of my Partial KeePass Database Export:

* the otp string field can be cleared, protecting your two-factor authentication secrets
* simplicity ;)

The other plugin looks good. Have a look and decide for yourself.