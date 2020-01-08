# Ucommerce Payment Provider Integrations

### What is this repository for? ###

This project contains all payment provider integrations shipped with Ucommerce out of the box. It is often easier to use a payment provider if the integration code is openly available. The code for the existing integrations is also great inspiration for those looking to create custom integrations with other payment providers not supported by Ucommerce out of the box.
Furthermore, the integrations being open-source allows for community pull-requests and the possibility to make custom fixes to them when necessary, to be used for a project.

### What does the repository contain? ###

* Full source code for all supported payment providers.
* Powershell script that can deploy the integrations as Ucommerce Apps, to a target folder.

### How do I deploy my custom fix as an app? ###

Simply make your changes to the source code, then run the "Deploy.As.Apps.ps1" The powershell script will override what's already in the apps folder, so it goes without saying you need to make sure you can restore to a state before you started making modifications should everything become a mess.

Before running the script make sure you have compiled the solution with either Debug or Release mode based on how you wish to deploy your changes. Otherwise the powershell script will fail as it tries to look for assemblies that does not exist.

example usage
```
.\Deploy.As.Apps.ps1 -TargetPath "c:\inetpub\u8\website\umbraco\ucommerce\apps" -Configuration Debug
```

### I only need a few of the providers ###

If you don't want to copy all the providers but only selected ones, you can either modify "$paymentProviders" array in the "Deploy.As.Apps.ps1" script and remove the ones you'd like to omit, or you can deploy all providers to a temporary location, and only copy the relevant ones into your website.

### Troubleshooting ###

Q: I deployed my app but nothing changes?  
A: In order to have Ucommerce load your application, you need to perform an application reset.

Q: If I can't override the existing app, won't my DLL clash with the existing one?  
A: You can disable the out of the box app (or any other apps) by adding a '.disabled' extension to the app folder's name. This will prevent any such issues.


