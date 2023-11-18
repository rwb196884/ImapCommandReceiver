# ImapCommandReceiver
Homa automation: recieve commands via email.

# Fucking security shit

```
ssh-keygen -t ed25519 -C "github.email@address"
# save as /home/rwb/.ssh/id_github
# Fuck knows what this bloody shit is
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/id_github
cat ~/.ssh/id_github.pub
# Paste it at https://github.com/settings/keys
git clone https://github.com/rwb196884/ImapCommandReceiver.git
git remote set-url origin git@github.com:rwb196884/ImapCommandReceiver.git
```
