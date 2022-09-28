# Ucommerce Payment Provider Integrations

NEW: Stripe payment provider integration. Courtesy of [@jamiehowarth0](https://github.com/jamiehowarth0). As of now there are a few manual steps necessary: 
- Creating the definition for the payment provider (to match the name of the service used - eg. "Stripe.com")
- Adding the definition fields used by the service (PublicKey, SecretKey, AcceptUrl, PaymentFormTemplate)

### What is this repository for? ###
DISCLAIMER: These implementations are made to serve as inpiration, or bases, and are not meant to be used in Production OOTB.
This project contains all payment provider integrations shipped with Ucommerce out of the box. It is often easier to use a payment provider if the integration code is openly available. The code for the existing integrations is also great inspiration for those looking to create custom integrations with other payment providers not supported by Ucommerce out of the box.
Furthermore, the integrations being open-source allows for community pull-requests and the possibility to make custom fixes to them when necessary, to be used for a project.

### What does the repository contain? ###

* Full source code for all supported payment providers.
* Powershell script that can deploy the integrations as Ucommerce Apps, to a target folder.

### How do I deploy my custom fix as an app? ###

Simply make your changes to the source code, then run the "build.ps1" with the "DeployToLocal" target (See example below). The build script will overwrite what's already in the apps folder, so it goes without saying you need to make sure you can restore to a state before you started making modifications should everything become a mess.

example usage
```
.\build.ps1 DeployToLocal --DeployDirectory "c:\inetpub\u8\website\umbraco\ucommerce\apps" --Configuration Debug
```

### I only need a few of the providers ###

If you don't want to copy all the providers but only selected ones, you can deploy all providers to a temporary location, and only copy the relevant ones into your website.

### Troubleshooting ###

Q: I deployed my app but nothing changes?  
A: In order to have Ucommerce load your application, you need to perform an application reset.

Q: If I can't override the existing app, won't my DLL clash with the existing one?  
A: You can disable the out of the box app (or any other apps) by adding a '.disabled' extension to the app folder's name. This will prevent any such issues.
