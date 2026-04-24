# Krosoft.Amqp.CLI

[![forthebadge](https://forthebadge.com/badges/built-with-love.svg)](https://forthebadge.com) [![forthebadge](https://forthebadge.com/badges/made-with-c-sharp.svg)](https://forthebadge.com)

Outil CLI pour gérer un broker ActiveMQ Artemis et des containers Docker via Portainer.

## Installation

```bash
dotnet tool install --global --add-source ./publish Krosoft.Amqp.CLI
```

## Profil

Les commandes s'appuient sur un fichier de profil JSON qui regroupe la configuration Portainer et AMQP.

```json
{
  "name": "mon-environnement",
  "portainer": {
    "url": "https://mon-serveur/portainer",
    "apiKey": "ptr_xxxxxxxxxxxxxxxxxxxx",
    "endpointId": 1
  },
  "amqp": {
    "url": "http://mon-serveur:8161",
    "username": "admin",
    "password": "admin",
    "brokerName": "0.0.0.0",
    "queues": ["MA_QUEUE_1", "MA_QUEUE_2"]
  },
  "containers": ["mon-service-api", "mon-service-worker"]
}
```

> **Auth Portainer** : utiliser `apiKey` (recommandé) **ou** `username` + `password`. L'API key se génère depuis _Profile → API keys_ dans l'interface Portainer.

## Commandes

### `info`

Affiche les informations du broker AMQP (version, uptime, connexions, queues…).

```bash
krosoft info --profile ./mon-profil.json
```

```
Version         : 2.31.2
Uptime          : 2 days 4 hours
Connexions      : 12
Addresses       : 8
Queues          : 14
Mémoire totale  : 1 073 741 824 bytes
```

### `queues`

Liste les statistiques de toutes les queues du broker.

```bash
krosoft queues --list --profile ./mon-profil.json
```

### `reset`

Arrête les containers définis dans le profil, purge les files AMQP, puis redémarre les containers.

```bash
krosoft reset --profile ./mon-profil.json
 .\src\Krosoft.Amqp.CLI\bin\Debug\net9.0\Krosoft.Amqp.CLI.exe reset --profile ./files/test.json
```

```
[1/3] Arrêt des containers...
  [OK] mon-service-api arrêté
  [OK] mon-service-worker arrêté

[2/3] Purge des files AMQP...
  Broker : 0.0.0.0
  [OK] MA_QUEUE_1 purgée (42 message(s))
  [OK] MA_QUEUE_2 purgée (0 message(s))

[3/3] Démarrage des containers...
  [OK] mon-service-api démarré
  [OK] mon-service-worker démarré

Reset terminé avec succès.
```
