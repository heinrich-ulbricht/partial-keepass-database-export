
# Partial KeePass Database Export
Export tagged entries to a new password database. The new database can use a different password.

# What does it do?
Every time you save your database a new database will be created and passwords with certain tags will be copied over (in KeePass you can mark any password entry with tags).

The new file is created in the same folder as your database file. If your database file is *passwords.kdbx* the new file will be named *passwords.partial-!readonly!.kdbx*. This file will be overwritten __every time__ you save the original database, so don't make any changes to the copy.

The new database can be protected with a password different from the original database's one. Key files and Windows account are not supported.

# What's the use case?
You have a giant password database in your secure bunker at home and don't want this database to ever leave your home. But sometimes you need *a few* passwords on the go. What now?

Use this plugin. Mark the passwords you need on the go with a certain tag and take the partial database exported by the plugin with you. If somebody steals this password database he only has a few passwords, not all.

# Configuration
Create a special password entry with the title *PartialExportConfig* in the root group. The plugin will search for this entry.

The password of this entry is the password for the exported database.

The tags of this entry are the tags the plugin looks for. Passwords having those tags will be exported. Multiple tags are supported, they are delimited by comma (,).

# Advanced Configuration
Note: this section assumes you created the special password entry *PartialExportConfig* as described above.

## Clear *otp* string field
For the *PartialExportConfig* entry create a string field with name *clearotp* to NOT export the *otp* string field of entries to the new database. The *otp* field is used by the [KeeOtp](https://keepass.info/plugins.html#keeotp) plugin to store the secret key for one-time password generation. If you use other devices to generate one-time passwords you might want to exclude those keys from the exported database to not expose them to attackers.

Note: the value of the *clearotp* field doesn't matter and can be empty.

# Limitations

* **the folder structure is not copied** - the exported database will have the root folder containing all exported passwords
* export to only one file
* file name and location can not be configured - it's always exported to the same folder as original database, with hardcoded name extension
