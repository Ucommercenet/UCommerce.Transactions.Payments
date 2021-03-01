[CmdletBinding()]
Param(
    [Parameter(Mandatory=$False)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

	[Parameter(Mandatory=$true, HelpMessage="Absolute path to folder where the payment provider integrations should be deployed to as Ucommerce apps.")]
	[string]$TargetPath = "path"
)

# Payment providers registered here will be moved by this Deploy script.
Function Get-Registered-Payment-Providers {
	$paymentProviders = @(
	    "Adyen",
	    "Authorizedotnet",
	    "Braintree",
	    "Dibs",
	    "EPay",
	    "EWay",
	    "GlobalCollect",
	    "Ideal",
	    "MultiSafepay",
	    "Netaxept",
	    "Ogone",
	    "Payer",
	    "PayEx",
	    "PayPal",
	    "Quickpay",
	    "SagePay",
	    "Schibsted",
	    "SecureTrading",
	    "Stripe",
	    "WorldPay"
    );
	return $paymentProviders;
}

function Get-ScriptDirectory { 
    Split-Path -parent $PSCommandPath 
}

function Run-It () {
	$scriptPath = Get-ScriptDirectory;
	$src = Resolve-Path "$scriptPath\..\src";

	foreach($element in Get-Registered-Payment-Providers){
	    $projectPath = "$src/Ucommerce.Transactions.Payments.$element";
	    
		Write-Host "Deploying > $element"

		if(Test-Path -Path $TargetPath\$element)
		{
			Remove-Item "$TargetPath\$element" -Force -Recurse
		}

		New-Item -Type Directory "$TargetPath\$element\bin" -Force
		Copy-Item "$projectPath\bin\$Configuration\Ucommerce.Transactions.Payments.$element.dll" -Destination "$TargetPath\$element\bin" -Force
		Copy-Item "$projectPath\Configuration" -Destination "$TargetPath\$element\Configuration" -Force -Recurse

		# DLL Dependency necessary for PayPal
		if($element -eq "PayPal"){
			Copy-Item "$projectPath\bin\$Configuration\paypal_base.dll" -Destination "$TargetPath\$element\bin" -Force
		}

		# DLL Dependency necessary for Braintree
		if($element -eq "Braintree"){
			Copy-Item "$projectPath\bin\$Configuration\Braintree.dll" -Destination "$TargetPath\$element\bin" -Force
			Copy-Item "$projectPath\bin\$Configuration\Newtonsoft.Json.dll" -Destination "$TargetPath\$element\bin" -Force
			Copy-Item "$projectPath\BraintreePaymentForm.htm" -Destination "$TargetPath\$element" -Force
		}

		# DLL Dependency necessary for Stripe
		if($element -eq "Stripe"){
			Copy-Item "$projectPath\bin\$Configuration\Stripe.net.dll" -Destination "$TargetPath\$element\bin" -Force
			Copy-Item "$projectPath\bin\$Configuration\Microsoft.Bcl.AsyncInterfaces.dll" -Destination "$TargetPath\$element\bin" -Force
			Copy-Item "$projectPath\StripePaymentForm.htm" -Destination "$TargetPath\$element" -Force
		}

		Write-Host "Deployed > $element"
		Write-Host "---------------------------------------------------------------------------------"
	}

	Write-Host "Finished deploying payment provider apps to $TargetPath";
}

Run-It