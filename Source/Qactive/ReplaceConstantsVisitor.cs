﻿using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Qactive
{
  internal sealed class ReplaceConstantsVisitor : ExpressionVisitor
  {
    public int ReplacedConstants { get; private set; }

    private readonly Type findType, findTypeDefinition;
    private readonly Func<object, Type, object> replacementSelector;
    private readonly Func<Type, Type> replacementTypeSelector;
    private readonly Action<Type, Type> genericTypeArgumentMismatchAction;

    private ReplaceConstantsVisitor(Type findTypeDefinition, Func<object, Type, object> replacementSelector, Func<Type, Type> replacementTypeSelector)
    {
      Contract.Requires(findTypeDefinition != null);
      Contract.Requires(findTypeDefinition.GetIsGenericTypeDefinition());
      Contract.Requires(replacementSelector != null);
      Contract.Requires(replacementTypeSelector != null);

      this.findTypeDefinition = findTypeDefinition;
      this.replacementSelector = replacementSelector;
      this.replacementTypeSelector = replacementTypeSelector;
    }

    private ReplaceConstantsVisitor(Type findType, Func<object, Type, object> replacementSelector, Func<Type, Type> replacementTypeSelector, Action<Type, Type> genericTypeArgumentMismatchAction)
    {
      Contract.Requires(findType != null);
      Contract.Requires(!findType.GetIsGenericTypeDefinition());
      Contract.Requires(replacementSelector != null);
      Contract.Requires(replacementTypeSelector != null);

      this.findType = findType;
      this.findTypeDefinition = findType.GetIsGenericType() ? findType.GetGenericTypeDefinition() : null;
      this.replacementSelector = replacementSelector;
      this.replacementTypeSelector = replacementTypeSelector;
      this.genericTypeArgumentMismatchAction = genericTypeArgumentMismatchAction;
    }

    public static ReplaceConstantsVisitor CreateForGenericTypeByDefinition(Type findTypeDefinition, Func<object, Type, object> replacementSelector, Func<Type, Type> replacementTypeSelector)
    {
      Contract.Requires(findTypeDefinition != null);
      Contract.Requires(replacementSelector != null);
      Contract.Requires(replacementTypeSelector != null);
      Contract.Ensures(Contract.Result<ReplaceConstantsVisitor>() != null);

      return new ReplaceConstantsVisitor(findTypeDefinition, replacementSelector, replacementTypeSelector);
    }

    public static ReplaceConstantsVisitor Create(Type findType, Func<object, Type, object> replacementSelector, Func<Type, Type> replacementTypeSelector, Action<Type, Type> genericTypeArgumentMismatchAction = null)
    {
      Contract.Requires(findType != null);
      Contract.Requires(replacementSelector != null);
      Contract.Requires(replacementTypeSelector != null);
      Contract.Ensures(Contract.Result<ReplaceConstantsVisitor>() != null);

      return new ReplaceConstantsVisitor(findType, replacementSelector, replacementTypeSelector, genericTypeArgumentMismatchAction);
    }

    public static ReplaceConstantsVisitor Create(Type findType, object replacement, Type replacementType, Action<Type, Type> genericTypeArgumentMismatchAction = null)
    {
      Contract.Requires(findType != null);
      Contract.Requires(replacementType != null);
      Contract.Ensures(Contract.Result<ReplaceConstantsVisitor>() != null);

      return Create(findType, (_, __) => replacement, _ => replacementType, genericTypeArgumentMismatchAction);
    }

    [ContractInvariantMethod]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Required for code contracts.")]
    private void ObjectInvariant()
    {
      Contract.Invariant(replacementTypeSelector != null);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
      var actualType = node.Value == null ? node.Type : node.Value.GetType();

      if (findType != null && findType.IsAssignableFrom(actualType))
      {
        ReplacedConstants++;

        return Expression.Constant(replacementSelector(node.Value, actualType), replacementTypeSelector(node.Type));
      }
      else if (actualType != null && actualType.GetIsGenericType() && actualType.GetGenericTypeDefinition() == findTypeDefinition)
      {
        if (genericTypeArgumentMismatchAction != null)
        {
          var different = actualType.GetGenericArguments()
            .Zip(findType.GetGenericArguments(), (actual, expected) => new { actual, expected })
            .Where(a => a.actual != a.expected);

          foreach (var pair in different)
          {
            genericTypeArgumentMismatchAction(pair.actual, pair.expected);
          }
        }

        ReplacedConstants++;

        return Expression.Constant(replacementSelector(node.Value, actualType), replacementTypeSelector(node.Type));
      }

      return base.VisitConstant(node);
    }
  }
}