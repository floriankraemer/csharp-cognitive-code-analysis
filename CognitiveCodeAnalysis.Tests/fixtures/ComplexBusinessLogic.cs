namespace CognitiveCodeAnalysis.Tests.Fixtures;

public class ComplexBusinessLogic
{
    private int state = 0;
    private string data = "";
    private bool isValid = false;

    public double ProcessComplexOrder(
        int orderId,
        string customerName,
        double orderAmount,
        string shippingAddress,
        string billingAddress,
        bool isExpressShipping,
        bool requiresInsurance,
        string paymentMethod)
    {
        double totalCost = 0.0;
        double shippingCost = 0.0;
        double insuranceCost = 0.0;
        double taxAmount = 0.0;
        double discountAmount = 0.0;

        if (orderAmount > 1000)
        {
            if (isExpressShipping)
            {
                if (requiresInsurance)
                {
                    insuranceCost = orderAmount * 0.05;
                }
                else
                {
                    insuranceCost = 0;
                }
            }
            else
            {
                if (requiresInsurance)
                {
                    insuranceCost = orderAmount * 0.03;
                }
                else
                {
                    insuranceCost = 0;
                }
            }
        }
        else
        {
            if (isExpressShipping)
            {
                insuranceCost = orderAmount * 0.02;
            }
            else
            {
                insuranceCost = 0;
            }
        }

        if (orderAmount > 500)
        {
            discountAmount = orderAmount * 0.1;
        }
        else if (orderAmount > 200)
        {
            discountAmount = orderAmount * 0.05;
        }
        else
        {
            discountAmount = 0;
        }

        if (isExpressShipping)
        {
            shippingCost = 25.0;
        }
        else
        {
            shippingCost = 10.0;
        }

        if (paymentMethod == "CreditCard")
        {
            taxAmount = orderAmount * 0.08;
        }
        else if (paymentMethod == "PayPal")
        {
            taxAmount = orderAmount * 0.07;
        }
        else
        {
            taxAmount = orderAmount * 0.06;
        }

        totalCost = orderAmount + shippingCost + insuranceCost + taxAmount - discountAmount;

        if (totalCost > 5000)
        {
            return totalCost * 1.1;
        }
        else if (totalCost > 2000)
        {
            return totalCost * 1.05;
        }
        else
        {
            return totalCost;
        }
    }

    public string ValidateAndProcessData(
        string input,
        int maxLength,
        bool allowSpecialChars,
        bool requireUppercase,
        bool requireNumbers)
    {
        string result = "";

        if (string.IsNullOrEmpty(input))
        {
            return "Error: Input is empty";
        }

        if (input.Length > maxLength)
        {
            return "Error: Input exceeds maximum length";
        }

        if (requireUppercase)
        {
            if (!input.Any(char.IsUpper))
            {
                return "Error: Input must contain uppercase letters";
            }
        }

        if (requireNumbers)
        {
            if (!input.Any(char.IsDigit))
            {
                return "Error: Input must contain numbers";
            }
        }

        if (!allowSpecialChars)
        {
            if (input.Any(c => !char.IsLetterOrDigit(c)))
            {
                return "Error: Input contains special characters";
            }
        }

        result = input.ToUpper();
        result = result.Trim();
        result = result.Replace(" ", "_");

        if (result.Length > 50)
        {
            result = result.Substring(0, 50);
        }

        return result;
    }
}

