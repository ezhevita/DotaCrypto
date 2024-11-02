# DotaCrypto

A high-performance C# library for decryption of [Dota 2 private match metadata](https://github.com/SteamDatabase/Protobufs/blob/bdb6250c11411be2bb30f65d6922c50c3763a0e2/dota2/dota_match_metadata.proto#L13).

## How to use

1. Retrieve match information, including private metadata key, e.g. by [using SteamKit](https://github.com/SteamRE/SteamKit/blob/a9b73a68f0c324abd78fc86c54fda022e09a8f23/Samples/022_DotaMatchRequest/Program.cs)
2. Download metadata file using the information received in the previous step and the following link: `http://replay{cluster}.valve.net/570/{match_id}_{replay_salt}.meta.bz2`
3. Uncompress the downloaded file and parse it as a [CDOTAMatchMetadataFile](https://github.com/SteamDatabase/Protobufs/blob/bdb6250c11411be2bb30f65d6922c50c3763a0e2/dota2/dota_match_metadata.proto#L9-L14) protobuf
4. Create the decryptor using acquired private metadata key and call the decryption method on the `private_metadata` field of the protobuf from the previous step
5. Disregard the first 4 bytes (IDK why) and uncompress it again as bz2 archive
6. Parse the result as a [CDOTAMatchPrivateMetadata](https://github.com/SteamDatabase/Protobufs/blob/bdb6250c11411be2bb30f65d6922c50c3763a0e2/dota2/dota_match_metadata.proto#L253-L353) protobuf

## Acknowledgements

The implementation is a port of the [Python version](https://github.com/thedanill/dota_crypto) by [@thedanill](https://github.com/thedanill), huge thanks to him!
