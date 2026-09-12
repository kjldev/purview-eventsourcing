namespace Purview.EventSourcing.SourceGenerator.Aggregate;

static partial class AggregateSourceEmitter
{
	static class CollectionCommandMethodEmitter
	{
		public static void Generate(CodeWriter writer, AggregateEventMethodInfo method)
		{
			var collectionEvent = method.CollectionEvent!;
			var parameter = method.AllParameters[0];
			var hookSuffix = GetHookName(method.EventType);
			var normalizeValidateSuffix = collectionEvent.NormalizeValidateHookSuffix;

			writer.Method(
				new(method.MethodName, method.ReturnType)
				{
					Accessibility = method.MethodAccessibility.ToTypeDeclarationAccessibility(),
					IsPartial = true,
					Parameters =
					[
						.. method.AllParameters.Select(static p => new ParameterDeclarationOptions(
							p.ParameterName,
							p.ParameterType
						)
						{
							IsParams = p.IsParams,
						}),
					],
				},
				writeBody =>
				{
					EmitCollectionGuard(writeBody, collectionEvent);

					if (collectionEvent.ParameterShape == CollectionParameterShape.Single)
						EmitSingleItemBody(
							writeBody,
							method,
							collectionEvent,
							parameter,
							hookSuffix,
							normalizeValidateSuffix
						);
					else
						EmitEnumerableBody(
							writeBody,
							method,
							collectionEvent,
							parameter,
							hookSuffix,
							normalizeValidateSuffix
						);

					EmitCollectionFinalization(writeBody, method, hookSuffix);
				}
			);

			writer.NewLine();

			EmitCollectionTrailingHookDeclarations(writer, method, collectionEvent, parameter, hookSuffix);
		}

		static void EmitCollectionGuard(CodeWriter writer, CollectionEventInfo collectionEvent)
		{
			writer.IfBlock(
				$"{collectionEvent.PropertyName} is null",
				block =>
					block.Throw(
						PurviewTypeLibrary.System.InvalidOperationException,
						$"Collection property '{collectionEvent.PropertyName}' cannot be null."
					)
			);
			writer.NewLine();
		}

		static void EmitSingleItemBody(
			CodeWriter writer,
			AggregateEventMethodInfo method,
			CollectionEventInfo collectionEvent,
			EventPropertyInfo parameter,
			string hookSuffix,
			string normalizeValidateSuffix
		)
		{
			writer.Assignment("var", "__itemValue", parameter.ParameterName);
			writer.MethodCall(
				$"OnNormalizing{normalizeValidateSuffix}",
				[new MethodCallArgumentOptions("__itemValue", ParameterModifier.Ref)]
			);
			writer.MethodCall(
				$"OnValidating{normalizeValidateSuffix}",
				[new MethodCallArgumentOptions("__itemValue", ParameterModifier.Ref)]
			);

			var isAddOperation = collectionEvent.Operation == CollectionMutationOperation.Add;
			if (isAddOperation && collectionEvent.IsSet)
			{
				writer.IfBlock(
					$"{collectionEvent.PropertyName}.Contains(__itemValue)",
					ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind)
				);
			}
			else if (!isAddOperation)
			{
				writer.IfBlock(
					$"!{collectionEvent.PropertyName}.Contains(__itemValue)",
					ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind)
				);
			}

			writer.Assignment(
				"var @event",
				new ObjectCreationOptions(method.EventType)
				{
					InitializerMembers = [new(parameter.PropertyName, "__itemValue")],
				}
			);

			writer.IfBlock(
				$"!ShouldApply{hookSuffix}(@event)",
				ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind)
			);

			writer.MethodCall(
				$"OnRaising{hookSuffix}",
				[new MethodCallArgumentOptions("__itemValue", ParameterModifier.Ref)]
			);
		}

		static void EmitEnumerableBody(
			CodeWriter writer,
			AggregateEventMethodInfo method,
			CollectionEventInfo collectionEvent,
			EventPropertyInfo parameter,
			string hookSuffix,
			string normalizeValidateSuffix
		)
		{
			var enumerableType = PurviewTypeLibrary.System.Collections.Generic.IEnumerable.MakeGeneric(
				collectionEvent.ElementType
			);

			writer.Assignment(enumerableType, "__itemsValue", parameter.ParameterName);
			writer.MethodCall(
				$"OnNormalizing{normalizeValidateSuffix}",
				[new MethodCallArgumentOptions("__itemsValue", ParameterModifier.Ref)]
			);
			writer.MethodCall(
				$"OnValidating{normalizeValidateSuffix}",
				[new MethodCallArgumentOptions("__itemsValue", ParameterModifier.Ref)]
			);
			writer.Assignment(
				"var",
				"__eventItems",
				$"__itemsValue as {collectionEvent.ElementType}[] ?? [.. __itemsValue]"
			);
			writer.IfBlock("__eventItems.Length == 0", ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind));

			var isAddOperation = collectionEvent.Operation == CollectionMutationOperation.Add;
			if (isAddOperation && collectionEvent.IsSet)
			{
				writer.Assignment("var", "__hasNewValues", "false");
				writer.Block(
					"foreach (var __item in __eventItems)",
					block =>
						block.IfBlock(
							$"!{collectionEvent.PropertyName}.Contains(__item)",
							ifBody =>
							{
								ifBody.Assignment("__hasNewValues", "true");
								ifBody.Line("break;");
							}
						)
				);
				writer.IfBlock("!__hasNewValues", ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind));
			}
			else if (!isAddOperation)
			{
				writer.Assignment("var", "__hasExistingValues", "false");
				writer.Block(
					"foreach (var __item in __eventItems)",
					block =>
						block.IfBlock(
							$"{collectionEvent.PropertyName}.Contains(__item)",
							ifBody =>
							{
								ifBody.Assignment("__hasExistingValues", "true");
								ifBody.Line("break;");
							}
						)
				);
				writer.IfBlock("!__hasExistingValues", ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind));
			}

			writer.Assignment(
				"var @event",
				new ObjectCreationOptions(method.EventType)
				{
					InitializerMembers = [new(parameter.PropertyName, "__eventItems")],
				}
			);

			writer.IfBlock(
				$"!ShouldApply{hookSuffix}(@event)",
				ifBody => EmitNoChangeReturn(ifBody, method.ReturnKind)
			);

			writer.MethodCall(
				$"OnRaising{hookSuffix}",
				[new MethodCallArgumentOptions("__itemsValue", ParameterModifier.Ref)]
			);
		}

		static void EmitCollectionFinalization(CodeWriter writer, AggregateEventMethodInfo method, string hookSuffix)
		{
			writer.NewLine();
			writer.MethodCall($"OnRaised{hookSuffix}", ["@event"]);
			writer.MethodCall("RecordAndApply", ["@event"]);
			EmitSuccessReturn(writer, method.ReturnKind);
		}

		static void EmitCollectionTrailingHookDeclarations(
			CodeWriter writer,
			AggregateEventMethodInfo method,
			CollectionEventInfo collectionEvent,
			EventPropertyInfo parameter,
			string hookSuffix
		)
		{
			var suppression = CreateCA1822Suppression();

			var normalizeValidateType =
				collectionEvent.ParameterShape == CollectionParameterShape.Single
					? collectionEvent.ElementType
					: TypeLibrary
						.System.Collections.Generic.IEnumerable.MakeGeneric(collectionEvent.ElementType)
						.AsTypeReference();
			ParameterDeclarationOptions normalizingParam = new(parameter.ParameterName, normalizeValidateType)
			{
				Modifier = ParameterModifier.Ref,
			};

			writer.PartialMethod(
				new($"OnRaising{hookSuffix}") { Attributes = [suppression], Parameters = [normalizingParam] }
			);

			writer.PartialMethod(
				new($"OnRaised{hookSuffix}")
				{
					Attributes = [suppression],
					Parameters = [new("@event", method.EventType)],
				}
			);

			writer.Method(
				new($"ShouldApply{hookSuffix}", PurviewTypeLibrary.System.Boolean)
				{
					Parameters = [new("@event", method.EventType)],
				},
				writeBody =>
				{
					writeBody.Assignment("var", "shouldApply", "true");
					writeBody.MethodCall($"OnShouldApply{hookSuffix}", ["@event", "ref shouldApply"]);
					writeBody.Return("shouldApply");
				}
			);

			writer.PartialMethod(
				new($"OnShouldApply{hookSuffix}")
				{
					Attributes = [suppression],
					Parameters =
					[
						new("@event", method.EventType),
						new("shouldApply", PurviewTypeLibrary.System.Boolean) { Modifier = ParameterModifier.Ref },
					],
				}
			);

			writer.PartialMethod(
				new($"OnApplied{hookSuffix}")
				{
					Attributes = [suppression],
					Parameters = [new("@event", method.EventType)],
				}
			);

			writer.NewLine();
		}
	}
}
