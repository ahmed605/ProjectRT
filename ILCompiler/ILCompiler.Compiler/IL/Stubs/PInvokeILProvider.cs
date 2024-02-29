// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Internal.IL.Stubs;
using Internal.TypeSystem.Ecma;

namespace Internal.IL
{
    /// <summary>
    /// Wraps the API and configuration for a particular PInvoke IL emitter. Eventually this will
    /// allow ILProvider to switch out its PInvoke IL generator with another, such as MCG.
    /// </summary>
    public class PInvokeILProvider : ILProvider
    {
        private readonly PInvokeILEmitterConfiguration _pInvokeILEmitterConfiguration;
        private readonly InteropStateManager _interopStateManager;

        public PInvokeILProvider(PInvokeILEmitterConfiguration pInvokeILEmitterConfiguration, InteropStateManager interopStateManager)
        {
            _pInvokeILEmitterConfiguration = pInvokeILEmitterConfiguration;
            _interopStateManager = interopStateManager;
        }

        public override MethodIL GetMethodIL(MethodDesc method)
        {
            var pregenerated = McgInteropSupport.TryGetPregeneratedPInvoke(method);
            if (pregenerated == null)
                return PInvokeILEmitter.EmitIL(method, _pInvokeILEmitterConfiguration, _interopStateManager);
            else
                return EcmaMethodIL.Create((EcmaMethod)pregenerated);
        }

        public MethodDesc GetCalliStub(MethodSignature signature)
        {
            return _interopStateManager.GetPInvokeCalliStub(signature);
        }
    }
}
