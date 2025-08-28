Setting up the Janitor - Discord Bot:

- Got to the Discord developer Portal. (https://discord.com/developers)
- Sign in with your Account.
- Create a New Application.
- Under OAuth2 Check the Bot.
![image](https://github.com/Fabloans/Janitor/assets/93011108/0c6414dd-92a9-4cca-8543-dc8d5dfda365)
- After that a window should appear. Check the following Permissions
![image](https://github.com/Fabloans/Janitor/assets/28175673/be634b00-f3dc-4c97-89c1-852b16d829be)

- Copy the generated link. This will be the link so the Bot can be added to your Server.

- Under the Category Bot Create and Copy the Bot Token. The token needs to be pasted in the "./Janitor.Core/config.json" document.
Example: "{
	"Token": "xxx"
}"

- After that you can run the Bot. Have fun.

When adding the bot to your server, it will create the essential roles "Role Manager" and "Friend".
There are two optional roles "Janitor" and "Guest" which you can create manually.

- The "Role Manager" role has permission to assign and remove the "Friend" role.
- The optional "Janitor" role has permission to only assign the "Friend" role.
- The optional "Guest" role will be assigned to users on join, and removed when assigning the "Friend" role (and vice verca).
- Automatic role assignment will be announced in the channel "#guest-lounge".
- Activity logs will be posted in the channel "#mod-log".
- The "Janitor", "Friend" and "Guest" roles are mutually exclusive.

In case you don't want the role assignment/revokation messages to be public, simply remove the bot's "Send messages" permission.
