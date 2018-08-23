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

	$paymentProviders = @("Adyen","Authorizedotnet","Braintree","Dibs","EPay","EWay","GlobalCollect","Ideal","MultiSafepay","Netaxept","Ogone","Payer","PayEx","PayPal","Quickpay","SagePay","Schibsted","SecureTrading","WorldPay");

	return $paymentProviders;
}


function Get-ScriptDirectory { 
    Split-Path -parent $PSCommandPath 
}

function Run-It () {
	$solution_file = "UCommerce.Transactions.Payments.sln";

	$scriptPath = Get-ScriptDirectory;

	$src = Resolve-Path "$scriptPath\..\src";

	foreach($element in Get-Registered-Payment-Providers){
		Write-Host "Deploying > $element"

		if(Test-Path -Path $TargetPath\$element)
		{
			Remove-Item "$TargetPath\$element" -Force -Recurse
		}

		New-Item -Type Directory "$TargetPath\$element\bin" -Force
		Copy-Item "$src/UCommerce.Transactions.Payments.$element\bin\$Configuration\UCommerce.Transactions.Payments.$element.dll" -Destination "$TargetPath\$element\bin" -Force
		Copy-Item "$src/UCommerce.Transactions.Payments.$element\Configuration" -Destination "$TargetPath\$element\Configuration" -Force -Recurse

		Write-Host "Deployed > $element"
		Write-Host "---------------------------------------------------------------------------------"
	}

	Write-Host "Finished deploying payment provider apps to $TargetPath";
	
}

Run-It