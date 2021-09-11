# PhoneSpamBlock

A tool to deal with pesky spam calls.

This tool uses:

https://www.linphone.org/technical-corner/liblinphone

https://github.com/bedefaced/sipdotnet

-api [Apiurl like https://www.tellows.com/num/]

-keyword [String to find a bad scored number]

-regex [regex to filter numbers]

-server [Your VOIP server]

-user [Username]

-pw [Password]

You can set comma/new line separated white and a black lists in txt files. (Change requires application restart)


These lists can contain nummers with *. 0049* blocks all calls from germany for example.


If the bot answers the call a random file from the sounds directory (not includet for copy right reseasons) is played and the call is recorded.


You can create folders inside the Sounds directory named like the start of a number to play certain files for special numbers only. \Sounds\0049 for media for calls from germany only.

More info at:

http://mrjumpingjack.de/phonespamblock/
